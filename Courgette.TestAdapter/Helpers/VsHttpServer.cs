using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using static CourgetteTestAdapter.Logger;

namespace CourgetteTestAdapter
{
	// used for receiving test results from browser
	class VsHttpServer : IDisposable
	{
		// browser sends http PUT with json content to following url:
		private string url = null;
		private HttpListener http;
		public static ushort Port = 0;

		public VsHttpServer(ushort vsListeningPort)
		{
			try
			{
				// it would be better to start using port 0 (to request ephemeral port), but HttpListener does NOT support this
				// Either use TcpListener or try KestrelHttpServer (has a lot of dependencies)
				http = new HttpListener();
				url = $"http://localhost:{vsListeningPort}/";
				http.Prefixes.Add(url);
				http.Start();
				Log($"Http Listener started on {url}");
				Port = vsListeningPort;
			}
			catch (Exception ex)
			{
				Log($"{ex.GetType().Name} occurred: {ex.Message}");
			}
		}

		public void Dispose()
		{
			Log("Stopping Listener");
			http.Close();
			Port = 0;
		}

		private void SendOptionsResponse(HttpListenerResponse response)
		{
			Log("Sending OPTIONS response to browser");
			response.AddHeader("Access-Control-Allow-Origin", "*");
			response.AddHeader("Access-Control-Allow-Headers", "*");
			response.AddHeader("Access-Control-Allow-Methods", "PUT, POST, OPTIONS");
			response.ContentLength64 = 0;
			response.StatusCode = 204;  // ok, no response content
			response.Close();
		}

		private void SendOkResponse(HttpListenerResponse response)
		{
			Log("Sending OK response to browser");
			response.AddHeader("Access-Control-Allow-Origin", "*");
			response.AddHeader("Access-Control-Allow-Headers", "*");
			response.AddHeader("Access-Control-Allow-Methods", "PUT, POST, OPTIONS");
			response.ContentLength64 = 0;
			response.StatusCode = 204;  // ok, no response content
			response.Close();
		}

		private async Task<string> GetRequestContentAsync(HttpListenerRequest request)
		{
			long length64 = request.ContentLength64;

			using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
			{
				string content = await reader.ReadToEndAsync().ConfigureAwait(false);
				Log($"Unit tests results received from browser, length read = {content.Length}, ContentLength64 = {length64}");
				return content;
			}
		}

		public async Task<string> GetResponseFromBrowserAsync()
		{
			//Debugger.Break();

			HttpListenerContext context = await http.GetContextAsync().ConfigureAwait(false);
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;

			string content = null;
			if (request.HttpMethod == "OPTIONS")
			{
				// if we receive OPTIONS, send correct CORS headers back
				SendOptionsResponse(response);

				// and get the next request
				content = await GetResponseFromBrowserAsync();
			}
			else
			{
				// get content of received request
				content = await GetRequestContentAsync(request).ConfigureAwait(false);

				// and send 204 response
				SendOkResponse(response);
			}

			return content;
		}

	}
}
