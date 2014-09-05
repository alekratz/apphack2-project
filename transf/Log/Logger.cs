using System;
using System.IO;
using System.Collections.Generic;

namespace transf.Log
{
	internal class Logger
	{
		// this is not set on purpose, so that it can be started at the beginning of the program
		public static Logger Instance { get; set; }

		public const string GROUP_APP = "Application";
		public const string GROUP_NET = "Net";
        public const string GROUP_FS = "Filesystem";

		#region Properties
		/// <summary>
		/// The output stream that the logger will print to.
		/// </summary>
		/// <value>A <see cref="TextWriter"/> object.</value>
		public TextWriter OutStream { get; set; }
		/// <summary>
		/// The groups that are allowed to print to the logger.
		/// </summary>
		/// <value>A hash set of the enabled groups.</value>
		public HashSet<string> EnabledGroups { get; set; }
		/// <summary>
		/// The highest level that the logger can print at. Default is info.
		/// </summary>
		/// <value>The log level.</value>
		public LogLevel LogLevel { get; set; }
		#endregion

		#region Constructor/Destructor
		public Logger (TextWriter outStream)
		{
			OutStream = outStream;
			EnabledGroups = new HashSet<string> ();
			// Add default groups
			EnabledGroups.Add (GROUP_APP);
			EnabledGroups.Add (GROUP_NET);
            EnabledGroups.Add (GROUP_FS);
#if DEBUG
			LogLevel = LogLevel.Debug;
#else
			LogLevel = LogLevel.Info;
#endif
			OutStream.WriteLine ("Application started");
		}

		~Logger ()
		{
			OutStream.WriteLine ("Application stopped");
		}
		#endregion

		/// <summary>
		/// Writes to the <see cref="OutStream"/> with the specified log level and group. 
		/// If the current <see cref="LogLevel"/> is higher than the log level specified, it will not be printed.
		/// If the group specified is not an enabled group, it will not be printed.
		/// </summary>
		/// <param name="level">The log level</param>
		/// <param name="group">The group</param>
		/// <param name="text">The log message</param>
		/// <param name="args">Any arguments to format into the log message</param>
		public void Write(LogLevel level, string group, string text, params object[] args)
		{
			// If the level is too low, or if the group isn't enabled
			if (level < LogLevel || !EnabledGroups.Contains(group))
				return;

			// Level, group, time, text
			const string LOG_FORMAT = "[{0}] <{1}> [{2}] {3}  {4}";

			string levelStr = level.ToString ();
			string timeStr = DateTime.Now.ToLongTimeString ();

			string logStr = string.Format (text, args);

			// Make sure that no other thread accesses this
			lock (OutStream)
			{
				OutStream.WriteLine (LOG_FORMAT, levelStr, group, timeStr, Environment.NewLine, logStr);
			}
		}

		public static void WriteVerbose(string group, string text, params object[] args)
		{
			Instance.Write (LogLevel.Verbose, group, text, args);
		}

		public static void WriteDebug(string group, string text, params object[] args)
		{
			Instance.Write (LogLevel.Debug, group, text, args);
		}

		public static void WriteInfo(string group, string text, params object[] args)
		{
			Instance.Write (LogLevel.Info, group, text, args);
		}

		public static void WriteWarning(string group, string text, params object[] args)
		{
			Instance.Write (LogLevel.Warning, group, text, args);
		}

		public static void WriteError(string group, string text, params object[] args)
		{
			Instance.Write (LogLevel.Error, group, text, args);
		}
	}
}

