// Newtonsoft.Json version 9.0.1 works in VS2017 15.3 and higher (don't update the nuget package to latest, you'll break everything)
// see here for more info: https://devblogs.microsoft.com/visualstudio/using-newtonsoft-json-in-a-visual-studio-extension/
using Newtonsoft.Json;

// use gateway pattern to allow easier change of chosen json library
namespace CourgetteTestAdapter
{
	static class Json
	{
		public static string Serialize<T>(T obj)
		{
			return JsonConvert.SerializeObject(obj);
		}

		public static T Deserialize<T>(string json)
		{
			return JsonConvert.DeserializeObject<T>(json);
		}
	}
}
