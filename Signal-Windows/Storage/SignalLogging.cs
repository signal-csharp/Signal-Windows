using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signal_Windows.Storage
{
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
            Debug.WriteLine(string.Format("[{0}] [{1}] ", logLevel, ClassName) + formatter(state, exception));
        }
    }

    class SignalLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new SignalConsoleLogger(categoryName);
        }

        public void Dispose()
        {
            Debug.WriteLine("disposing SignalLoggerProvider");
        }
    }
}
