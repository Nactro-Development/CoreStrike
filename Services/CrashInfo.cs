using System;

namespace CoreStrike
{
    public class CrashInfo
    {
        public string AppVersion { get; set; }
        public string WindowsVersion { get; set; }
        public string DotNetVersion { get; set; }
        public string Architecture { get; set; }

        public DateTime Time { get; set; }

        public string ExceptionType { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string InnerException { get; set; }

        // NEW
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string CurrentCulture { get; set; }
        public long Memory { get; set; }
        public int ProcessId { get; set; }



        public string AppPath { get; set; }
        public string CommandLine { get; set; }
        public int ProcessorCount { get; set; }
        public bool Is64BitOS { get; set; }
        public bool Is64BitProcess { get; set; }

    }

}