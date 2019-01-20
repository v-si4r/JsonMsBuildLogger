using System;
using System.IO;
using System.Security;
using JsonMsBuildLogger.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

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
        /// The MsBuild log file stream
        /// </summary>
        private StreamWriter streamWriter;

        /// <summary>
        /// The JSON writer used to write into MsBuild log file stream <see cref="streamWriter"/>
        /// </summary>
        private JsonWriter jsonWriter;

        /// <summary>
        /// The flag indicates whether that logger is disposed
        /// </summary>
        private bool disposed = false;

        // ?
        private int indent;

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
#endif
            try
            {
                // open the file
                this.streamWriter = new StreamWriter(logFile);

                // initialize JSON.NET writer
                this.jsonWriter = new JsonTextWriter(this.streamWriter);
                this.jsonWriter.Formatting = Formatting.Indented;
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


            eventSource.ProjectStarted += new ProjectStartedEventHandler(HandleEventSourceProjectStarted);
            //eventSource.TaskStarted += new TaskStartedEventHandler(eventSource_TaskStarted);
            //eventSource.MessageRaised += new BuildMessageEventHandler(eventSource_MessageRaised);
            //eventSource.WarningRaised += new BuildWarningEventHandler(eventSource_WarningRaised);
            //eventSource.ErrorRaised += new BuildErrorEventHandler(eventSource_ErrorRaised);
            //eventSource.ProjectFinished += new ProjectFinishedEventHandler(eventSource_ProjectFinished);
        }

        #region CP
        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            // BuildErrorEventArgs adds LineNumber, ColumnNumber, File, amongst other parameters
            string line = String.Format(": ERROR {0}({1},{2}): ", e.File, e.LineNumber, e.ColumnNumber);
            WriteLineWithSenderAndMessage(line, e);
        }

        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            // BuildWarningEventArgs adds LineNumber, ColumnNumber, File, amongst other parameters
            string line = String.Format(": Warning {0}({1},{2}): ", e.File, e.LineNumber, e.ColumnNumber);
            WriteLineWithSenderAndMessage(line, e);
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            // BuildMessageEventArgs adds Importance to BuildEventArgs
            // Let's take account of the verbosity setting we've been passed in deciding whether to log the message
            if ((e.Importance == MessageImportance.High && IsVerbosityAtLeast(LoggerVerbosity.Minimal))
                || (e.Importance == MessageImportance.Normal && IsVerbosityAtLeast(LoggerVerbosity.Normal))
                || (e.Importance == MessageImportance.Low && IsVerbosityAtLeast(LoggerVerbosity.Detailed))
                )
            {
                WriteLineWithSenderAndMessage(String.Empty, e);
            }
        }

        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            // TaskStartedEventArgs adds ProjectFile, TaskFile, TaskName
            // To keep this log clean, this logger will ignore these events.
        }

        private void HandleEventSourceProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            // ProjectStartedEventArgs adds ProjectFile, TargetNames
            // Just the regular message string is good enough here, so just display that.
            //WriteLine(String.Empty, e);
            //indent++;

            this.WriteLine(MsBuildEventType.ProjectStarted, string.Empty, e);
        }

        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            // The regular message string is good enough here too.
            indent--;
            WriteLine(String.Empty, e);
        }

        /// <summary>
        /// Write a line to the log, adding the SenderName and Message
        /// (these parameters are on all MSBuild event argument objects)
        /// </summary>
        private void WriteLineWithSenderAndMessage(string line, BuildEventArgs e)
        {
            if (0 == String.Compare(e.SenderName, "MSBuild", true /*ignore case*/))
            {
                // Well, if the sender name is MSBuild, let's leave it out for prettiness
                WriteLine(line, e);
            }
            else
            {
                WriteLine(e.SenderName + ": " + line, e);
            }
        }

        /// <summary>
        /// Just write a line to the log
        /// </summary>
        private void WriteLine(string line, BuildEventArgs e)
        {
            for (int i = indent; i > 0; i--)
            {
                streamWriter.Write("\t");
            }
            streamWriter.WriteLine(line + e.Message);
        }

        /// <summary>
        /// Just write a line to the log
        /// </summary>
        private void WriteLine(MsBuildEventType eventType, string line, BuildEventArgs e)
        {
            try
            {
                var bundle = new JsonBundle
                {
                    EventType = eventType,
                    Message = line,
                    BuildEventArgs = e
                };

                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(this.jsonWriter, bundle);
            }
            catch (Exception ex)
            {
                this.diagnostics.WriteException(ex);
            }
        }

        /// <summary>
        /// Shutdown() is guaranteed to be called by MSBuild at the end of the build, after all 
        /// events have been raised.
        /// </summary>
        public override void Shutdown()
        {
            this.Dispose();
        }

        #endregion

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
