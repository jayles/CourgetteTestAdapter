using System;
using System.Diagnostics;
using System.Threading.Tasks;
using static CourgetteTestAdapter.Logger;

// from nuget Microsoft.TestPlatform.ObjectModel
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace CourgetteTestAdapter
{
	class Browser
	{
		private static readonly object portLock = new Object();
		private static ushort _chromeDebuggingPortCounter = 9222;

		private ushort _chromeDebuggingPort;
		private IRunContext _runContext;
		private string _wwwroot;
		private string _testFileUrl;

		public Browser(IRunContext runContext, string wwwroot, string testFileUrl)
		{
			_runContext = runContext;
			_wwwroot = wwwroot;
			_testFileUrl = testFileUrl;

			// cycle through ports, to allow overlapping async operations / parallel execution
			lock (portLock)
			{
				_chromeDebuggingPort = _chromeDebuggingPortCounter++;
			}
		}

		public static void LaunchIISExpress(string iisConfigFile)
		{
			// TODO: should get these from XML config file
			const string powerShellExe = @"C:\Windows\System32\WindowsPowerShell\v1.0\PowerShell.exe";
			const string iisExePath = @"C:\Program Files\IIS Express\iisexpress.exe";
			string iisArgs = $"/config:{iisConfigFile}";

			// use PowerShell so console window can be hidden
			//	PowerShell -Command "Start-Process -FilePath '...' -ArgumentList ..."
			Process[] processes = Process.GetProcessesByName("iisexpress");
			if (processes.Length == 0)
			{
				//string PowerShellArgs = $"-Command \"Start-Process .\\iisexpress.exe -WindowStyle Hidden\"";
				string powerShellArgs = $"-Command \"Start-Process -WindowStyle Hidden -FilePath '{iisExePath}' -ArgumentList {iisArgs}\"";
				var process = Process.Start(powerShellExe, powerShellArgs);
			}
		}

		public async Task LaunchAsync()
		{
			//Debugger.Break();

			// ensure http listener has started (HttpServer is started synchronously, so not currently required, but listener code might change to async startup)
			while (VsHttpServer.Port == 0)
				await Task.Delay(100).ConfigureAwait(false);

			ushort vsListeningPort = VsHttpServer.Port;

			// Note that Chrome only supports single debugging port per user profile, if you want
			// multiple concurrent instances you need to configure the --user-data-dir startup param
			//	https://stackoverflow.com/questions/52797350/multithreading-chromedriver-does-not-open-url-in-second-window/52799116#52799116

			// for list of some of the Chrome startup params, look here:
			// https://github.com/GoogleChrome/chrome-launcher/blob/master/docs/chrome-flags-for-tools.md

			// --remote-debugging-port option keeps chrome open until we close it
			//string args = $"--user-data-dir={Logger.LogPath}{_chromeDebuggingPort} --enable-automation --disable-extensions --no-sandbox --disable-gpu --remote-debugging-port={_chromeDebuggingPort}";
			string args;
			if (_runContext.IsBeingDebugged)
				args = $"--user-data-dir={Logger.LogPath}{_chromeDebuggingPort} --remote-debugging-port={_chromeDebuggingPort}";
			else
				args = $"--user-data-dir={Logger.LogPath}{_chromeDebuggingPort} --enable-automation --disable-extensions --no-sandbox --headless --disable-gpu --remote-debugging-port={_chromeDebuggingPort}";
			Log($"Chrome cmd line args are : {args}");

			// start chrome with no initial URL specified
			const string chromeExe = @"c:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
			var process = Process.Start(chromeExe, args);

			// next get websocket endpoint uri using http via the debugging port
			// note that trying to get this from within the browser is problematic because you will be hitting CORS violations, CORS applies to http only, not web sockets
			CdpClient cdp = new CdpClient(_chromeDebuggingPort);
			string wsBrowserUri = await cdp.CdpConfig.LoadConfigAsync();

			// run specified test file and also pass in VS listening port and WS endpoint uri
			string url = $"http://localhost:10202/TestRunner.html?wwwroot={_wwwroot}&testFileUrl={_testFileUrl}&vsPort={vsListeningPort}&wsUri={wsBrowserUri}";
			string response;
			response = await cdp.EnablePageEventsAsync();
			response = await cdp.EnableNetworkEventsAsync();
			response = await cdp.NavigatePageAsync(url);
		}

		public async Task CloseAsync()
		{
			if (_runContext.IsBeingDebugged)
			{
				Log("CloseAsync(): Debug mode detected, leaving browser open");
				return;
			}
			else
				Log("CloseAsync(): Attempting to close browser...");

			// note you can't use any of the following, since the process to start Chrome
			// opens the addtional window on the main chrome process and then exits immediately
			//process.CloseMainWindow();
			//process.Close();
			//process.Kill();

			// this won't work either because Chrome consists of 7 different processes, this will only kill the windowed process
			//foreach (Process process in Process.GetProcessesByName("chrome"))
			//{
			//	if (process.MainWindowHandle == IntPtr.Zero)
			//		continue;
			//	if (process.MainWindowTitle.StartsWith("Courgette Test Runner"))
			//		process.CloseMainWindow();
			//}

			// use CDP to close Chrome (should also work for Chromium, Firefox, Edge 76+ and Opera, but NOT Safari)
			Log($"Attempting to connect to browser using CDP on port {_chromeDebuggingPort}");
			CdpClient cdp = new CdpClient(_chromeDebuggingPort);

			// wait for browser to close
			string result = await cdp.CloseBrowserAsync().ConfigureAwait(false);
		}

	}
}
