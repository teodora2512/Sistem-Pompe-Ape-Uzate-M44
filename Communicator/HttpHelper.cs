using DataModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Communicator
{
    public class HttpHelper
    {
        private const string ApiUrl = "http://localhost:49570/api/simulator";

        public static List<ProcessStatusEvent> GetDataFromWebAPI()
        {
            string html = string.Empty;

            HttpWebRequest request=(HttpWebRequest)WebRequest.Create(ApiUrl);
            request.AutomaticDecompression= DecompressionMethods.GZip;

            using (HttpWebResponse response=(HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<List<ProcessStatusEvent>>(html);

        }

        public static void PostDataToWebAPI(ProcessStatusEvent postData)
        {
            WebRequest request = WebRequest.Create(ApiUrl);
            request.Method = "POST";

            string postDataJson = JsonConvert.SerializeObject(postData);
            byte[] byteArray = Encoding.UTF8.GetBytes(postDataJson);

            request.ContentType = "application/json";
            request.ContentLength = byteArray.Length;

            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            WebResponse webResponse = request.GetResponse();
            using (dataStream = webResponse.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                reader.ReadToEnd();
            }
            webResponse.Close();
        }
    }
}
