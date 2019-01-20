namespace JsonMsBuildLogger.Diagnostics
{
    /// <summary>
    /// Provides an instance of specified diagnostics logger
    /// </summary>
    internal static class DiagnosticsProvider
    {
        /// <summary>
        /// Gets the logger based on chosen type
        /// </summary>
        /// <returns>Logger instance</returns>
        public static IDiagnosticsLogger GetLogger(/* TODO: define a type */)
        {
            return new FileDiagnosticsLogger();
        }
    }
}
