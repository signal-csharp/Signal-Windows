using libsignalservice;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Signal_Windows.Storage
{
    public class SignalLogging
    {
        public static void SetupLogging(bool ui)
        {
            if (ui)
            {
                var console = new SignalConsoleLoggerProvider();
                var file = new SignalFileLoggerProvider(ApplicationData.Current.LocalCacheFolder.Path + @"\Signal-Windows.ui.log", "UI");
                LibsignalLogging.LoggerFactory.AddProvider(console);
                LibsignalLogging.LoggerFactory.AddProvider(file);
            }
            else
            {
                LibsignalLogging.LoggerFactory.AddProvider(new SignalFileLoggerProvider(ApplicationData.Current.LocalCacheFolder.Path + @"\Signal-Windows.bg.log", "BG"));
            }
        }
    }

    class SignalConsoleLogger : ILogger
    {
        private readonly string ClassName;
        public SignalConsoleLogger(string categoryName)
        {
            ClassName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Debug.WriteLine(string.Format("{0:s} [{1}] [{2}] ", DateTime.UtcNow, logLevel, ClassName) + formatter(state, exception));
        }
    }

    class SignalConsoleLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new SignalConsoleLogger(categoryName);
        }

        public void Dispose()
        {

        }
    }

    class SignalFileLogger : ILogger
    {
        private readonly string ClassName;
        private readonly SignalFileLoggerProvider Provider;

        public SignalFileLogger(string categoryName, SignalFileLoggerProvider provider)
        {
            ClassName = categoryName;
            Provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Provider.Log(string.Format("[{0}] [{1}] ", logLevel, ClassName) + formatter(state, exception));
        }
    }

    public class SignalFileLoggerProvider : ILoggerProvider
    {
        private static string UILog = ApplicationData.Current.LocalCacheFolder.Path + @"\Signal-Windows.ui.log";
        private readonly string Filename;
        private readonly string OldFilename;
        private readonly string Prefix;
        private const int MaxLogSize = 256 * 1024;
        private static object Lock = new object();

        public SignalFileLoggerProvider(string filename, string prefix)
        {
            Filename = filename;
            OldFilename = Filename + ".old";
            Prefix = prefix;
        }

        public void TruncateLog()
        {
            try
            {
                var length = new FileInfo(Filename).Length;
                if (length > MaxLogSize)
                {
                    if (File.Exists(OldFilename))
                    {
                        File.Delete(OldFilename);
                    }
                    File.Move(Filename, OldFilename);
                    File.AppendAllText(Filename, $"{DateTime.UtcNow.ToString("s")} [SignalFileLoggerProvider] truncated log file\n");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("SignalFileLoggerProvider failed to truncate file: {0}", e));
            }
        }

        public void Log(string line)
        {
            lock (Lock)
            {
                try
                {
                    TruncateLog();
                    File.AppendAllText(Filename, $"{DateTime.UtcNow.ToString("s")} [{Prefix}] {line}\n");
                }
                catch (Exception e)
                {
                    Debug.WriteLine(string.Format("SignalFileLoggerProvider failed to write: {0}", e));
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SignalFileLogger(categoryName, this);
        }

        public void Dispose()
        {
        }

        public static void ForceAddUILog(string msg)
        {
            lock (Lock)
            {
                try
                {
                    File.AppendAllText(UILog, msg);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(string.Format("SignalFileLoggerProvider failed to write: {0}", e));
                }
            }
        }

        public static void ExportUILog(StorageFile file)
        {
            lock(Lock)
            {
                FileIO.WriteTextAsync(file, "").AsTask().Wait();
                CachedFileManager.DeferUpdates(file);
                var writer = file.OpenStreamForWriteAsync().Result;
                try
                {
                    var oldLog = File.OpenRead(ApplicationData.Current.LocalCacheFolder.Path + @"\Signal-Windows.ui.log.old");
                    MoveFileContent(oldLog, writer);
                    oldLog.Dispose();
                } catch (Exception) { }
                try
                {
                    var newLog = File.OpenRead(ApplicationData.Current.LocalCacheFolder.Path + @"\Signal-Windows.ui.log");
                    MoveFileContent(newLog, writer);
                    newLog.Dispose();
                }
                catch (Exception) { }
                Windows.Storage.Provider.FileUpdateStatus status = CachedFileManager.CompleteUpdatesAsync(file).AsTask().Result;
            }
        }

        private static void MoveFileContent(Stream source, Stream destination)
        {
            byte[] buffer = new byte[1024];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, read);
            }
            destination.Flush();
        }
    }
}
