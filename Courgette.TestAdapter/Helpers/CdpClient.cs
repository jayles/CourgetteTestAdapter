using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using static CourgetteTestAdapter.Logger;

// Note that the full-fat .NET MS WebSockets class does NOT work on Windows 7
// .NET Core 2.1+ DOES support WebSockets on Windows 7
using System.Net.WebSockets;

// nuget System.Net.WebSockets.Client.Managed
using ClientWebSocket = System.Net.WebSockets.Managed.ClientWebSocket;  // use this for Windows 7 functionality

// hard to find docs for the underlying CDP protocol (as opposed to API implementatiosn of the protocol, e.g. puppeteer)
// Have a look here:
//
//	https://gist.github.com/umaar/ebc170660f15aa894fa4880f4b76e77d
//	https://github.com/aslushnikov/getting-started-with-cdp
//	https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
//
// [1] before WebSockets call need to perform http GET to below:
//	GET http://localhost:9222/json/version	to get webSocketDebuggerUrl field
//	can also call use GET http://localhost:9222/json
//
// [2] for browser commands, connect to something like this (do http GET first to find required url)
//	ws://localhost:9222/devtools/browser/7ff1875e-cdf6-488b-b051-6a5ecac6c622
//
// C# APIs
//	https://github.com/ststeiger/ChromeDevTools
//
// JavaScript CDP client APIs:
//	https://github.com/cyrus-and/chrome-remote-interface
//	https://github.com/TracerBench/chrome-debugging-client (TypeScript support)
//
// NodeJs API:
//	https://github.com/GoogleChrome/puppeteer
//
namespace CourgetteTestAdapter
{
	public class CdpClient
	{
		private ushort _chromeDebuggingPort = 0;
		public CdpConfig CdpConfig;

		// ctor
		public CdpClient(ushort chromeDebuggingPort)
		{
			_chromeDebuggingPort = chromeDebuggingPort;
			CdpConfig = new CdpConfig(chromeDebuggingPort);
		}

		private async Task<string> LoadConfigAsync()
		{
			// load config
			string wsEndpoint = await CdpConfig.LoadConfigAsync().ConfigureAwait(false);
			return wsEndpoint;
		}

		//private async Task<WebSocketReceiveResult> SendCmdAsync(CdpMsg msg)
		private async Task<string> SendCmdAsync(CdpMsg msg)
		{
			try
			{
				using (ClientWebSocket ws = new ClientWebSocket())
				{
					Log($"CdpClient.SendMsg(): Attempting to send cmd {msg.method}, port is {_chromeDebuggingPort}");

					// load config
					await CdpConfig.LoadConfigAsync().ConfigureAwait(false);

					byte[] receiveBytes = new byte[65536];
					var receiveBuffer = new ArraySegment<byte>(receiveBytes);
					ws.Options.SetBuffer(65536, 65536); // (receive 64KB, send 64KB)

#if DEBUG
					var tokenSource = new CancellationTokenSource(600000);	// 10 mins
#else
					var tokenSource = new CancellationTokenSource(10000);		// 10 secs
#endif
					CancellationToken cancellationToken = tokenSource.Token;

					Uri wsUri;
					if (msg.method.StartsWith("Browser"))
						wsUri = this.CdpConfig.BrowserWsUri;
					else
						wsUri = this.CdpConfig.PageWsUri;

					Log($"CdpClient.SendMsg(): Attempting to connect to uri {wsUri}, port is {_chromeDebuggingPort}...");
					await ws.ConnectAsync(wsUri, cancellationToken).ConfigureAwait(false);
					Log($"CdpClient.SendMsg(): ...Connected to to uri {wsUri}, port is {_chromeDebuggingPort}");

					string json = Json.Serialize<CdpMsg>(msg);
					byte[] bytes = Encoding.UTF8.GetBytes(json);
					ArraySegment<byte> sendBuffer = new ArraySegment<byte>(bytes);

					Log($"About to send CDP cmd {msg.method} to port {_chromeDebuggingPort}...");
					await ws.SendAsync(sendBuffer, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
					WebSocketReceiveResult result = await ws.ReceiveAsync(receiveBuffer, cancellationToken).ConfigureAwait(false);
					Log($"...received CDP result for cmd {msg.method}, result = {Json.Serialize(result)}, port is {_chromeDebuggingPort}");

					// retrive result from buffer
					if (result.MessageType != WebSocketMessageType.Text)
						return $"Unexpected message type: {result.MessageType}";

					string msgText = Encoding.UTF8.GetString(receiveBytes, 0, result.Count);
					Log($"{msg.method}(): returned result {msgText}, port is {_chromeDebuggingPort}");

					return msgText;
				}
			}
			catch (Exception ex)
			{
				Log($"Exception occurred: {ex.ToString()}");
				return null;
			}
		}

		//public async Task<WebSocketReceiveResult> CloseBrowserAsync()
		public async Task<string> CloseBrowserAsync()
		{
			// close browser
			var parameters = new Dictionary<string, object>(); // create empty params list
			//CdpMsg msg = new CdpMsg { id = id++, method = "Browser.close", @params = parms };
			CdpMsg msg = new CdpMsg("Browser.close", parameters);
			string result = await SendCmdAsync(msg).ConfigureAwait(false);
			return result;
		}

		//public async Task<WebSocketReceiveResult> NavigatePageAsync(string url)
		public async Task<string> NavigatePageAsync(string url)
		{
			// navigate to specified url
			var parameters = new Dictionary<string, object>(); // create empty params list
			parameters.Add("url", url);
			//CdpMsg msg = new CdpMsg { id = id++, method = "Page.navigate", @params = parms };
			CdpMsg msg = new CdpMsg("Page.navigate", parameters);
			string result = await SendCmdAsync(msg).ConfigureAwait(false);
			return result;
		}

		public async Task<string> EnablePageEventsAsync()
		{
			var parameters = new Dictionary<string, object>(); // create empty params list
			CdpMsg msg = new CdpMsg("Page.enable", parameters);
			string result = await SendCmdAsync(msg).ConfigureAwait(false);
			return result;
		}

		public async Task<string> EnableNetworkEventsAsync()
		{
			var parameters = new Dictionary<string, object>(); // create empty params list
			CdpMsg msg = new CdpMsg("Network.enable", parameters);
			string result = await SendCmdAsync(msg).ConfigureAwait(false);
			return result;
		}

		//public async Task<WebSocketReceiveResult> DiscoverTargets()
		//{
		//	var parms = new Dictionary<string, object>();
		//	parms.Add("discover", true);
		//	CdpMsg msg = new CdpMsg { id = id++, method = "Target.setDiscoverTargets", @params = parms };
		//	WebSocketReceiveResult result = await SendCmd(msg).ConfigureAwait(false);
		//	return result;
		//}

		//public async Task<WebSocketReceiveResult> RuntimeEvaluate()
		//{
		//	var parms = new Dictionary<string, object>();
		//	parms.Add("expression", "`'The current URL is:' + location.href`");
		//	CdpMsg msg = new CdpMsg { id = id++, method = "Runtime.evaluate", @params = parms };
		//	WebSocketReceiveResult result = await SendCmd(msg).ConfigureAwait(false);
		//	return result;
		//}
	}
}
