using System;

namespace JsonMsBuildLogger.Diagnostics
{
    /// <summary>
    /// Implements diagnostic logger behavior
    /// </summary>
    /// <seealso cref="JsonMsBuildLogger.Diagnostics.IDiagnosticsLogger" />
    internal abstract class DiagnosticsLogger : IDiagnosticsLogger
    {
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Writes the exception occurred during logging
        /// </summary>
        /// <param name="ex">The exception.</param>
        public void WriteException(Exception ex)
        {
            this.Write($"[{DateTime.Now:O}]\t{ex}");
        }

        /// <summary>
        /// Writes the message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void WriteMessage(string message)
        {
            this.Write($"[{DateTime.Now:O}]\t{message}");
        }

        /// <summary>
        /// Writes the diagnostic data.
        /// </summary>
        /// <param name="data">The data which is should be written.</param>
        protected abstract void Write(string data);
    }
}
