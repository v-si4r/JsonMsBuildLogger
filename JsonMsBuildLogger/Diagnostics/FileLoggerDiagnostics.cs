using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace JsonMsBuildLogger.Diagnostics
{
    /// <summary>
    /// Writes all incoming data into file
    /// </summary>
    /// <seealso cref="JsonMsBuildLogger.Diagnostics.DiagnosticsLogger" />
    internal class FileDiagnosticsLogger : DiagnosticsLogger
    {
        #region fields

        /// <summary>
        /// The diagnostic log file stream
        /// </summary>
        private StreamWriter diagnosticStreamWriter;

        /// <summary>
        /// The flag indicates whether that logger is disposed
        /// </summary>
        private bool disposed = false;

        #endregion

        #region ctor       

        /// <summary>
        /// Finalizes an instance of the <see cref="FileDiagnosticsLogger"/> class.
        /// </summary>
        ~FileDiagnosticsLogger()
        {
            Dispose(false);
        }

        #endregion

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>      
        public override void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Writes the diagnostic data.
        /// </summary>
        /// <param name="data">The data which is should be written.</param>      
        protected override void Write(string data)
        {
            try
            {
                this.GetStream()?.WriteLine(data);
            }
            catch (Exception ex)
            {
                // something went totally wrong!
                throw new LoggerException($"Diagnostic logging is failed: {ex.Message}", ex.InnerException);
            }
        }

        private StreamWriter GetStream()
        {
            if (this.diagnosticStreamWriter == null)
            {
                // Open the file
                this.diagnosticStreamWriter = new StreamWriter($"{DateTime.Now:yyyyMMddHHmmss}Diag.log");
            }

            return this.diagnosticStreamWriter;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // dispose managed resources
                    this.diagnosticStreamWriter?.Dispose();
                    this.diagnosticStreamWriter = null;
                }

                // dispose unmanaged resources
                this.disposed = true;
            }
        }
    }
}
