using libsignalservice.messages;
using Signal_Windows.Models;
using Signal_Windows.Signal;
using Signal_Windows.Storage;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Signal_Windows.ViewModels
{
    public partial class MainPageViewModel
    {
        private BlockingCollection<Tuple<SignalMessage[], bool>> DBQueue = new BlockingCollection<Tuple<SignalMessage[], bool>>(new ConcurrentQueue<Tuple<SignalMessage[], bool>>());

        private void HandleDBQueue()
        {
            Debug.WriteLine("HandleDBQueue starting...");
            CancellationToken token = CancelSource.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Tuple<SignalMessage[], bool> t = DBQueue.Take(token);
                    lock (SignalDBContext.DBLock)
                    {
                        using (var ctx = new SignalDBContext())
                        {
                            foreach (var message in t.Item1)
                            {
                                if (t.Item2)
                                {
                                    message.Author = ctx.Contacts.Single(b => b.UserName == message.AuthorUsername);
                                }
                                else
                                {
                                    message.Author = null;
                                }
                                ctx.Messages.Add(message);
                                if (message.Type == (uint)SignalMessageType.Incoming || message.DeviceId != (int)LocalSettings.Values["DeviceId"])
                                {
                                    if (message.Attachments != null && message.Attachments.Count > 0)
                                    {
                                        ctx.SaveChanges();
                                        HandleDBAttachments(message, ctx);
                                    }
                                }
                            }
                            ctx.SaveChanges();
                            if (t.Item2)
                            {
                                IncomingMessageSavedEvent.Set();
                            }
                            else
                            {
                                OutgoingQueue.Add(t.Item1[0]);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
            }
            DBOffSwitch.Set();
            Debug.WriteLine("HandleDBQueue finished");
        }

        private void HandleDBAttachments(SignalMessage message, SignalDBContext ctx)
        {
            int i = 0;
            foreach (var sa in message.Attachments)
            {
                sa.FileName = "attachment_" + message.Id + "_" + i;
                Task.Run(() =>
                {
                    try
                    {
                        DirectoryInfo di = Directory.CreateDirectory(Manager.localFolder + @"\Attachments");
                        using (var cipher = File.Open(Manager.localFolder + @"\Attachments\" + sa.FileName + ".cipher", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        using (var plain = File.OpenWrite(Manager.localFolder + @"\Attachments\" + sa.FileName))
                        {
                            SignalManager.MessageReceiver.retrieveAttachment(new SignalServiceAttachmentPointer(sa.StorageId, sa.ContentType, sa.Key, sa.Relay), plain, cipher);
                            //TODO notify UI
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine(e.StackTrace);
                    }
                });
                i++;
            }
        }
    }
}