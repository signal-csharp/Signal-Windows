using Signal_Windows.Lib;
using Signal_Windows.Models;
using Signal_Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Signal_Windows
{
    public class SignalWindowsFrontend : ISignalFrontend
    {
        public CoreDispatcher Dispatcher { get; set; }
        public ViewModelLocator Locator { get; set; }
        public SignalWindowsFrontend(CoreDispatcher dispatcher, ViewModelLocator locator, int viewId)
        {
            Dispatcher = dispatcher;
            Locator = locator;
        }
        public void AddOrUpdateConversation(SignalConversation conversation)
        {
            Locator.MainPageInstance.AddOrUpdateConversation(conversation);
        }

        public void HandleIdentitykeyChange(LinkedList<SignalMessage> messages)
        {
            Locator.MainPageInstance.HandleIdentitykeyChange(messages);
        }

        public void HandleMessage(SignalMessage message)
        {
            Locator.MainPageInstance.HandleMessage(message);
        }

        public void HandleMessageUpdate(SignalMessage updatedMessage)
        {
            Locator.MainPageInstance.HandleMessageUpdate(updatedMessage);
        }

        public void ReplaceConversationList(List<SignalConversation> conversations)
        {
            Locator.MainPageInstance.ReplaceConversationList(conversations);
        }

        public void HandleAuthFailure()
        {
            // TODO
        }
    }
}
