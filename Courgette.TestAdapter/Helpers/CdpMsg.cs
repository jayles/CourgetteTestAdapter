using System.Collections.Generic;

namespace CourgetteTestAdapter
{
	public class CdpMsg
	{
		private static int _id = 1;
		public int id;
		public string method;
		public Dictionary<string, object> @params;

		public CdpMsg(string methodName, Dictionary<string, object> parameters)
		{
			id = ++_id;
			method = methodName;
			@params = parameters;
		}
	}
}
