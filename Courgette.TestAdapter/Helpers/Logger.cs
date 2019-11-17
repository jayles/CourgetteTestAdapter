using System;
using System.IO;

namespace CourgetteTestAdapter
{
	public static class Logger
	{
		private static readonly object fileLock = new object();
		public static string LogPath;
		public static string LogFilename;

		static Logger()
		{
			LogPath = Path.GetTempPath() + "courgette\\";
			LogFilename = LogPath + "courgette.log";

			// create logging dir if it doesn't exist
			Directory.CreateDirectory(Path.GetDirectoryName(LogFilename));
			Log("\r\nLogger started...");
		}

		// must be reentrant
		public static void Log(string text)
		{
			string datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			string logMsg = $"{datetime} {text}\r\n";

			// a simple lock will do for now, for better performance use a ConcurrentQueue<T>, for best performance use a ring buffer
			lock (fileLock)
			{
				File.AppendAllText(LogFilename, logMsg);
			}
		}
	}
}
