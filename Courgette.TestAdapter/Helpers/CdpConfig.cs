using System;
using System.Threading.Tasks;
using static CourgetteTestAdapter.Logger;

namespace CourgetteTestAdapter
{
	public class CdpConfig
	{
		public dynamic BrowserConfig { get; private set; } = null;
		public dynamic PageConfig { get; private set; } = null;
		public Uri BrowserWsUri { get; private set; } = null;
		public Uri PageWsUri { get; private set; } = null;
		private ushort _chromeDebuggingPort;

		public CdpConfig(ushort chromeDebuggingPort)
		{
			_chromeDebuggingPort = chromeDebuggingPort;
		}

		public async Task<string> LoadConfigAsync()
		{
			// return if config is already loaded
			if (BrowserConfig != null && BrowserConfig.BrowserWsUri != null)
				return BrowserConfig.ChromeWsUri.ToString();

			// get the Browser websocket config from http://localhost:9222/json/version
			VsHttpClient httpClient = new VsHttpClient(_chromeDebuggingPort);
			var jsonConfig = await httpClient.GetCdpWsEndpointAsync("/json/version").ConfigureAwait(false);
			BrowserConfig = Json.Deserialize<dynamic>(jsonConfig);
			string uriString = BrowserConfig["webSocketDebuggerUrl"];
			BrowserWsUri = new Uri(uriString);
			Log($"CdpConfig(): Browser ws uri is : {BrowserWsUri}");

			// get the Page websocket config from http://localhost:9222/json
			jsonConfig = await httpClient.GetCdpWsEndpointAsync("/json").ConfigureAwait(false);
			PageConfig = Json.Deserialize<dynamic>(jsonConfig);
			uriString = PageConfig[0]["webSocketDebuggerUrl"];
			PageWsUri = new Uri(uriString);
			Log($"CdpConfig(): Page ws uri is : {PageWsUri}");

			return BrowserWsUri.ToString();
		}

	}
}
