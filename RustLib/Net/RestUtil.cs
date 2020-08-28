using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace RustLib.Net
{
	public class RestUtil
	{
		private class JsonUtil
		{
			public string api_key = "";
			public object json = null;

			public JsonUtil(string api_key, object obj)
			{
				this.api_key = api_key;
				this.json = obj;
			}
		}

		private string base_url;
		private string api_key;

		public RestUtil(string base_url, string api_key)
		{
			this.base_url = base_url;
			this.api_key = api_key;
		}

		public T PostJson<T>(string resource_path, object obj)
		{
			string data = PostJson(resource_path, obj);
			T ret_val = JsonConvert.DeserializeObject<T>(data);
			return ret_val;
		}

		public string PostJson(string resource_path, object obj)
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(base_url + "/" + resource_path);
			request.ContentType = "application/json";
			request.Method = "POST";

			JsonUtil util = new JsonUtil(api_key, obj);

			string json = JsonConvert.SerializeObject(util);

			using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
			{
				writer.Write(json);
				writer.Flush();
				writer.Close();
			}

			string result;
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();

			using (StreamReader reader = new StreamReader(response.GetResponseStream()))
			{
				result = reader.ReadToEnd();
			}

			return result;
		}
	}
}
