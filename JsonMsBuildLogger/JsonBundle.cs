using Microsoft.Build.Framework;

namespace JsonMsBuildLogger
{
    public class JsonBundle
    {
        public MsBuildEventType EventType { get; set; }

        public string Message { get; set; }

        public BuildEventArgs BuildEventArgs { get; set; }
    }
}
