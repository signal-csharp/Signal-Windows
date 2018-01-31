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

    class SignalFileLoggerProvider : ILoggerProvider
    {
        private readonly string Filename;
        private readonly string Prefix;
        private StreamWriter Writer;
        private object Lock = new object();

        public SignalFileLoggerProvider(string filename, string prefix)
        {
            Filename = filename;
            Prefix = prefix;
            Open();
        }

        public void Open()
        {
            Writer = File.CreateText(Filename);
        }

        public void Log(string line)
        {
            lock (Lock)
            {
                try
                {
                    Writer.WriteLine($"{DateTime.UtcNow.ToString("s")} [{Prefix}] {line}");
                    Writer.Flush();
                }
                catch(Exception e)
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
            lock(Lock)
            {
                Writer?.Dispose();
                Writer = null;
            }
        }
    }
}
