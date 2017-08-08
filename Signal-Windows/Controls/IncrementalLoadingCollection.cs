using Signal_Windows.Models;
using Signal_Windows.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Signal_Windows.Controls
{
    public class MessagesDataSource : IIncrementalSource<SignalMessage>
    {
        private List<SignalMessage> messages;

        public MessagesDataSource()
        {

        }
        public IEnumerable<SignalMessage> GetPagedItems(string threadId, int pageIndex, int pageSize)
        {
            Debug.WriteLine("GetPagedItems "+pageIndex+" "+pageSize);
            return SignalDBContext.GetMessagesLocked(threadId, pageIndex, pageSize);
        }
    }
    public interface IIncrementalSource<T>
    {
        IEnumerable<T> GetPagedItems(string threadId, int pageIndex, int pageSize);
    }
    public class IncrementalLoadingCollection<T, I> : ObservableCollection<I>, ISupportIncrementalLoading
        where T: IIncrementalSource<I>, new()
    {
        private T source;
        private int itemsPerPage;
        private bool hasMoreItems;
        private int currentPage;
        public string ThreadId;

        public IncrementalLoadingCollection(int itemsPerPage = 20)
        {
            this.source = new T();
            this.itemsPerPage = itemsPerPage;
            this.hasMoreItems = true;
        }

        public bool HasMoreItems
        {
            get { return hasMoreItems; }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var dispatcher = Window.Current.Dispatcher;

            return Task.Run(() =>
            {
                uint resultCount = 0;
                var result = source.GetPagedItems(ThreadId, currentPage++, itemsPerPage);
                Debug.WriteLine("read " + result?.Count() + "items");
                if (result == null || result.Count() == 0)
                {
                    hasMoreItems = false;
                }
                else
                {
                    resultCount = (uint)result.Count();
                    
                    dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        foreach (I item in result)
                            this.Add(item);
                    }).AsTask().Wait();
                }
                return new LoadMoreItemsResult() { Count = resultCount };
            }).AsAsyncOperation<LoadMoreItemsResult>();
        }
    }
}
