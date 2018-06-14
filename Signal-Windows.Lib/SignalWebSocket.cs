using libsignalservice;
using libsignalservice.push.exceptions;
using libsignalservice.websocket;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Signal_Windows.Lib
{
    public class SignalWebSocketFactory : ISignalWebSocketFactory
    {
        public ISignalWebSocket CreateSignalWebSocket(CancellationToken token, Uri uri)
        {
            return new SignalWebSocket(token, uri);
        }
    }

    class SignalWebSocket : ISignalWebSocket
    {
        private readonly ILogger Logger = LibsignalLogging.CreateLogger<SignalWebSocket>();
        private readonly MessageWebSocket WebSocket;
        private readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly Uri SignalWSUri;
        private readonly CancellationToken Token;
        public event EventHandler<SignalWebSocketClosedEventArgs> Closed;
        public event EventHandler<SignalWebSocketMessageReceivedEventArgs> MessageReceived;

        public SignalWebSocket(CancellationToken token, Uri uri)
        {
            WebSocket = new MessageWebSocket();
            WebSocket.MessageReceived += WebSocket_MessageReceived;
            WebSocket.Closed += WebSocket_Closed;
            Token = token;
            SignalWSUri = uri;
        }

        private void WebSocket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Closed?.Invoke(sender, new SignalWebSocketClosedEventArgs() { Code = args.Code, Reason = args.Reason });
            Logger.LogWarning("WebSocket_Closed() {0} ({1})", args.Code, args.Reason);
        }

        private async void WebSocket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (var data = args.GetDataStream())
                {
                    MessageReceived.Invoke(sender, new SignalWebSocketMessageReceivedEventArgs() { Message = data.AsStreamForRead() });
                }
            }
            catch(Exception e)
            {
                Logger.LogError("WebSocket_MessageReceived failed: {0}\n{1}", e.Message, e.StackTrace);
                try
                {
                    await ConnectAsync();
                }
                catch (TaskCanceledException) { }
            }
        }

        public void Close(ushort code, string reason)
        {
            Logger.LogTrace("Closing SignalWebSocket connection");
            WebSocket.Close(code, reason);
        }

        public async Task ConnectAsync()
        {
            var locked = await SemaphoreSlim.WaitAsync(0, Token); // ensure no threads are reconnecting at the same time
            if (locked)
            {
                while (!Token.IsCancellationRequested)
                {
                    try
                    {
                        await WebSocket.ConnectAsync(SignalWSUri).AsTask(Token);
                        SemaphoreSlim.Release();
                        break;
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("(403)"))
                        {
                            SemaphoreSlim.Release();
                            throw new AuthorizationFailedException("OWS server rejected authorization.");
                        }
                        Logger.LogError("ConnectAsync() failed: {0}\n{1}", e.Message, e.StackTrace); //System.Runtime.InteropServices.COMException (0x80072EE7)
                        await Task.Delay(10 * 1000);
                    }
                }
            }
        }

        public void Dispose()
        {
            WebSocket.Dispose();
        }

        public async Task SendMessage(byte[] data)
        {
            using (var dataWriter = new DataWriter(WebSocket.OutputStream))
            {
                dataWriter.WriteBytes(data);
                await dataWriter.StoreAsync();
                dataWriter.DetachStream();
            }
        }
    }
}
