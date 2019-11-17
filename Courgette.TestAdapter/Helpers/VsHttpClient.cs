using System;
using System.Net.Http;
using System.Threading.Tasks;
using static CourgetteTestAdapter.Logger;

namespace CourgetteTestAdapter
{
	// used for opening CDP session with browser
	public class VsHttpClient
	{
		private static readonly HttpClient http = new HttpClient();
		private ushort _chromeDebuggingPort;// = 9222;

		public VsHttpClient(ushort chromeDebuggingPort)
		{
			_chromeDebuggingPort = chromeDebuggingPort;
		}

		public async Task<string> GetCdpWsEndpointAsync(string path)
		{
			// browser config url: http://localhost:9222/json/version
			// page config url:    http://localhost:9222/json
			Uri remoteUri = new Uri($"http://localhost:{_chromeDebuggingPort}{path}");
			try
			{
				HttpResponseMessage response = await http.GetAsync(remoteUri).ConfigureAwait(false);
				string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				return content;
			}
			catch (Exception ex)
			{
				Log($"GetCdpConfig(): exception thrown - {ex.ToString()}");
				throw;
			}
		}

	}
}
