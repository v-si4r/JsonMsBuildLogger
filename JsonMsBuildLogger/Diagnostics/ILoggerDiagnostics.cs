using System;

namespace JsonMsBuildLogger.Diagnostics
{
    /// <summary>
    /// Diagnostic logger during logging of MsBuild process
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal interface IDiagnosticsLogger: IDisposable
    {
        /// <summary>
        /// Writes the message.
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteMessage(string message);

        /// <summary>
        /// Writes the exception occurred during logging
        /// </summary>
        /// <param name="ex">The exception.</param>
        void WriteException(Exception ex);
    }
}
