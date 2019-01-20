using System;
using System.IO;
using System.Security;
using JsonMsBuildLogger.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        #region constants

        /// <summary>
        /// The expected command text which is acceptable for current logger type
        /// </summary>
        private readonly static string ExpectedCommandText = $"-logger:{nameof(JsonMsBuildLogger)}.dll;<<json file path>> is expected";

        #endregion

        #region fields

        /// <summary>
        /// MsBuild log file stream
        /// </summary>
        private StreamWriter streamWriter;

        /// <summary>
        /// JSON writer used to write into MsBuild log file stream <see cref="streamWriter"/>
        /// </summary>
        private JsonWriter jsonWriter;

        /// <summary>
        /// Default JSON serializer
        /// </summary>
        private JsonSerializer serializer;

        /// <summary>
        /// The flag indicates whether that logger is disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// The flag indicates whether that first event is logged
        /// </summary>
        private bool firstEventIsLogged = false;

#if DEBUG

        IDiagnosticsLogger diagnostics = null;

#endif

        #endregion

        #region ctor

        /// <summary>
        /// Finalizes an instance of the <see cref="JsonFileLogger"/> class.
        /// </summary>
        ~JsonFileLogger()
        {
            Dispose(false);
        }

        #endregion

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


#if DEBUG
            this.diagnostics = DiagnosticsProvider.GetLogger();
            this.diagnostics.WriteMessage($"Parameters: {this.Parameters}");
            this.diagnostics.WriteMessage($"Verbosity level: {this.Verbosity}");
#endif     

            try
            {
                // open the file
                this.streamWriter = new StreamWriter(logFile);

                // start JSON logging
                this.BeginJsonArray();

                // initialize JSON.NET writer
                this.jsonWriter = new JsonTextWriter(this.streamWriter);
                this.jsonWriter.Formatting = Formatting.Indented;

                // initialize serializer
                this.serializer = new JsonSerializer();
            }
            catch (Exception ex)
            {
#if DEBUG
                // let's look at it
                this.diagnostics.WriteException(ex);
#endif

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

            // Occurs when a build raises any other type of build event.
            // Not sensitive to chosen MsBuild Verbosity level
            // Default logger behavior 
            eventSource.AnyEventRaised += this.HandleEventSourceEvent;
        }

        /// <summary>
        /// Handles the event source event of any type
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="BuildEventArgs"/> instance containing the event data.</param>
        private void HandleEventSourceEvent(object sender, BuildEventArgs e)
        {
#if DEBUG
            // log all events
            this.diagnostics.WriteMessage($"{e.SenderName}: {e.GetType().Name} {e.Message}");
#endif

            try
            {
                switch (this.Verbosity)
                {
                    case LoggerVerbosity.Quiet:
                        {
                            if (e is BuildWarningEventArgs || e is BuildErrorEventArgs)
                            {
                                break;
                            }

                            return;
                        }

                    case LoggerVerbosity.Minimal:
                        {
                            if (e is BuildWarningEventArgs || e is BuildErrorEventArgs)
                            {
                                break;
                            }

                            var buildMessage = e as BuildMessageEventArgs;
                            if (buildMessage?.Importance == MessageImportance.High)
                            {
                                break;
                            }

                            return;
                        }

                    case LoggerVerbosity.Normal:
                        {
                            if (e is BuildWarningEventArgs || e is BuildErrorEventArgs)
                            {
                                break;
                            }

                            var buildMessage = e as BuildMessageEventArgs;
                            if (buildMessage?.Importance == MessageImportance.High || buildMessage?.Importance == MessageImportance.Normal)
                            {
                                break;
                            }

                            return;
                        }

                   case LoggerVerbosity.Detailed:
                        {
                            if (e is BuildWarningEventArgs || e is BuildErrorEventArgs || e is BuildMessageEventArgs)
                            {
                                break;
                            }

                            return;
                        }
                }

                // write JSON element line break after previous element
                this.AddJsonArrayLineBreak();

                // write build event
                var rootObject = new JObject();
                rootObject.Add(e.GetType().Name, JObject.FromObject(e));
                this.serializer.Serialize(this.jsonWriter, rootObject);
            }
            catch (Exception ex)
            {
                this.diagnostics.WriteException(ex);
            }
        }

        /// <summary>
        /// Begins the JSON array at log file
        /// </summary>
        private void BeginJsonArray()
        {
            this.streamWriter.Write('[');
        }

        /// <summary>
        /// Adds the JSON array line break.
        /// </summary>
        private void AddJsonArrayLineBreak()
        {
            if (this.firstEventIsLogged)
            {
                this.streamWriter.WriteLine(',');
            }
            else
            {
                this.firstEventIsLogged = true;
            }
        }

        /// <summary>
        /// Closes the JSON array at log file
        /// </summary>
        private void CloseJsonArray()
        {
            this.streamWriter.Write(']');
        }

        /// <summary>
        /// Shutdown() is guaranteed to be called by MSBuild at the end of the build, after all 
        /// events have been raised.
        /// </summary>
        public override void Shutdown()
        {
            this.Dispose();
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

                    this.CloseJsonArray();

                    this.jsonWriter?.Close();
                    this.jsonWriter = null;

                    this.streamWriter?.Dispose();
                    this.streamWriter = null;
#if DEBUG
                    this.diagnostics?.Dispose();
                    this.diagnostics = null;
#endif                   
                }

                // dispose unmanaged resources
                this.disposed = true;
            }
        }
    }
}
