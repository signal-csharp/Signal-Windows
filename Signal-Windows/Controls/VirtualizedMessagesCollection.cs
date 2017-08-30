using Signal_Windows.Models;
using Signal_Windows.Storage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Controls
{
    public class SignalMessageContainer
    {
        public SignalMessage Message;
        public int Index;
        public SignalMessageContainer(SignalMessage message, int index)
        {
            Message = message;
            Index = index;
        }
    }

    public class VirtualizedCollection : IList, INotifyCollectionChanged
    {
        private readonly static int PAGE_SIZE = 50;
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        private Dictionary<int, IList<SignalMessageContainer>> Cache = new Dictionary<int, IList<SignalMessageContainer>>();
        private SignalConversation Conversation;

        public VirtualizedCollection(SignalConversation c)
        {
            Conversation = c;
        }

        private static int GetPageIndex(int itemIndex)
        {
            return itemIndex / PAGE_SIZE;
        }


        public object this[int index]
        {
            get
            {
                int inpageIndex = index % PAGE_SIZE;
                int pageIndex = GetPageIndex(index);
                if (!Cache.ContainsKey(pageIndex))
                {
                    Debug.WriteLine($"cache miss {pageIndex}");
                    Cache[pageIndex] = SignalDBContext.GetMessagesLocked(Conversation, pageIndex * PAGE_SIZE, PAGE_SIZE);
                }
                var page = Cache[pageIndex];
                return page[inpageIndex];
            }
            set => throw new NotImplementedException();
        }

        public bool IsFixedSize => false;

        public bool IsReadOnly => false;

        public int Count => (int)Conversation.MessagesCount;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        /// <summary>
        /// "Adds" a SignalMessageContainer to this virtualized collection.</summary>
        /// <remarks>
        /// The method may (if incoming) or may not (if outgoing) already be present in the database, so we explicitly insert at the correct position in the cache line.
        /// Count is mapped to the SignalConversation's MessagesCount, so callers must update appropriately before calling this method, and no async method must be called in between.</remarks>
        /// <param name="value">The object to add to the VirtualizedMessagesCollection.</param>
        /// <returns>The position into which the new element was inserted, or -1 to indicate that the item was not inserted into the collection.</returns>
        public int Add(object value)
        {
            var message = value as SignalMessageContainer;
            int inpageIndex = message.Index % PAGE_SIZE;
            int pageIndex = GetPageIndex(message.Index);
            Debug.WriteLine($"VirtualizedCollection.Add Id={message.Message.Id} Index={message.Index} PageIndex={pageIndex} InpageIndex={inpageIndex} ");
            if (!Cache.ContainsKey(pageIndex))
            {
                Cache[pageIndex] = SignalDBContext.GetMessagesLocked(Conversation, pageIndex * PAGE_SIZE, PAGE_SIZE);
            }
            Cache[pageIndex].Insert(inpageIndex, message);
            if (Cache[pageIndex].IndexOf(message) != inpageIndex  || message.Index != Count-1)
            {
                throw new InvalidOperationException("VirtualizedCollection is in an inconsistent state!");
            }
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, message, message.Index));
            return message.Index;
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(object value)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(object value)
        {
            SignalMessageContainer smc = value as SignalMessageContainer;
            if (smc != null)
            {
                Debug.WriteLine($"IndexOf returning {smc.Index}");
                return smc.Index;
            }
            return -1;
        }

        public void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }
    }
}
