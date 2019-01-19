using System;
using System.IO;
using System.Security;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace JsonMsBuildLogger
{
    /// <summary>
    /// This logger provides JSON data logging into file based on chosen verbosity level.
    /// <para>
    ///     This logger will derive from the Microsoft.Build.Utilities.Logger class, 
    ///     which provides it with getters and setters for Verbosity and Parameters, 
    ///     and a default empty Shutdown() implementation.
    /// </para>
    /// </summary>
    /// <seealso cref="Microsoft.Build.Utilities.Logger" />
    public sealed class JsonFileLogger : Logger, IDisposable
    {
        /// <summary>
        /// The expected command text which is acceptable for current logger type
        /// </summary>
        private readonly static string ExpectedCommandText = $"-logger:{nameof(JsonMsBuildLogger)}.dll;<<json file path>> is expected";

        private StreamWriter streamWriter;
        private bool disposed = false;

        /// <summary>
        /// Finalizes an instance of the <see cref="JsonFileLogger"/> class.
        /// </summary>
        ~JsonFileLogger()
        {
            Dispose(false);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Initializes the specified event source.
        /// </summary>
        /// <param name="eventSource">The event source.</param>
        public override void Initialize(IEventSource eventSource)
        {
            // get log file path from incoming parameters
            var logFile = GetLogFilePath(this.Parameters);

            try
			{
				// Open the file
				this.streamWriter = new StreamWriter(logFile);
			}
			catch (Exception ex)
			{
				if
				(
					ex is UnauthorizedAccessException
					|| ex is ArgumentNullException
					|| ex is PathTooLongException
					|| ex is DirectoryNotFoundException
					|| ex is NotSupportedException
					|| ex is ArgumentException
					|| ex is SecurityException
					|| ex is IOException
				)
				{
					throw new LoggerException("Failed to create log file: " + ex.Message);
				}
				else
				{
					// Unexpected failure
					throw;
				}
			}
        }

        /// <summary>
        /// Gets the log file path.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="LoggerException">        
        /// Log file was not set.
        /// or
        /// Too many parameters passed.
        /// </exception>
        private static string GetLogFilePath(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                throw new LoggerException($"Log file was not set. {ExpectedCommandText}");
            }

            var parametersArr = parameters.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

            if (parametersArr.Length == 0)
            {
                throw new LoggerException($"Log file was not set. {ExpectedCommandText}");
            }

            if (parametersArr.Length > 1)
            {
                throw new LoggerException($"Too many parameters passed. {ExpectedCommandText}");
            }

            return parametersArr[0];
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
                    this.streamWriter?.Dispose();
                    this.streamWriter = null;
                }

                // dispose unmanaged resources
                this.disposed = true;
            }
        }
    }
}
