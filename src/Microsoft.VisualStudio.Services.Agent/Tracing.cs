
using Microsoft.VisualStudio.Services.Agent.Util;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.VisualStudio.Services.Agent
{
    public sealed class Tracing : IDisposable
    {
        private ISecretMasker _secretMasker;

        private TraceSource _traceSource;

        public Tracing(string name, ISecretMasker secretMasker, SourceSwitch sourceSwitch, TextWriterTraceListener traceListener)
        {
            ArgUtil.NotNull(secretMasker, nameof(secretMasker));
            _secretMasker = secretMasker;
            _traceSource = new TraceSource(name);
            _traceSource.Switch = sourceSwitch;

            // Remove the default trace listener.
            if (_traceSource.Listeners.Count > 0 &&
                _traceSource.Listeners[0] is DefaultTraceListener)
            {
                _traceSource.Listeners.RemoveAt(0);
            }

            _traceSource.Listeners.Add(traceListener);
        }

        public void Info(string message)
        {
            Trace(TraceEventType.Information, message);
        }

        public void Info(string format, params object[] args)
        {
            Trace(TraceEventType.Information, StringUtil.Format(format, args));
        }

        public void Info(object item)
        {
            string json = JsonConvert.SerializeObject(item, Formatting.Indented);
            Trace(TraceEventType.Information, json);
        }

        public void Error(Exception exception)
        {
            Trace(TraceEventType.Error, exception.ToString());
        }

        // Do not remove the non-format overload.
        public void Error(string message)
        {
            Trace(TraceEventType.Error, message);
        }

        public void Error(string format, params object[] args)
        {
            Trace(TraceEventType.Error, StringUtil.Format(format, args));
        }

        // Do not remove the non-format overload.
        public void Warning(string message)
        {
            Trace(TraceEventType.Warning, message);
        }

        public void Warning(string format, params object[] args)
        {
            Trace(TraceEventType.Warning, StringUtil.Format(format, args));
        }

        // Do not remove the non-format overload.
        public void Verbose(string message)
        {
            Trace(TraceEventType.Verbose, message);
        }

        public void Verbose(string format, params object[] args)
        {
            Trace(TraceEventType.Verbose, StringUtil.Format(format, args));
        }

        public void Verbose(object item)
        {
            string json = JsonConvert.SerializeObject(item, Formatting.Indented);
            Trace(TraceEventType.Verbose, json);
        }

        public void Entering([CallerMemberName] string name = "")
        {
            Trace(TraceEventType.Verbose, name);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Trace(TraceEventType eventType, string message)
        {
            ArgUtil.NotNull(_traceSource, nameof(_traceSource));
            _traceSource.TraceEvent(eventType, 0, _secretMasker.MaskSecrets(message));
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _traceSource.Flush();
                _traceSource.Close();
            }
        }
    }
}
