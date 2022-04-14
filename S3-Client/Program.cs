using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace S3_Client
{
    class Program
    {
        private const string filePath = @"C:\Work\upload.xml";
        private const string downloadPath = @"C:\Work\download.xml";
        static HttpClient client = new HttpClient();
        static void Main(string[] args)
        {
            RunUploaderAsync().GetAwaiter().GetResult();
            RunDownloadAsync().GetAwaiter().GetResult();
        }

        public static async Task RunUploaderAsync()
        {
            var url = await GeneratePreSignedURL("upload");
            UploadObject(url);
        }

        public static async Task RunDownloadAsync()
        {
            var url = await GeneratePreSignedURL("download");
            DownloadObject(url);
        }

        private static void UploadObject(string url)
        {
            HttpWebRequest httpRequest = WebRequest.Create(url) as HttpWebRequest;
            httpRequest.Method = "PUT";
            using (Stream dataStream = httpRequest.GetRequestStream())
            {
                var buffer = new byte[8000];
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int bytesRead = 0;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        dataStream.Write(buffer, 0, bytesRead);
                    }
                }
            }
            HttpWebResponse response = httpRequest.GetResponse() as HttpWebResponse;
        }

        private static void DownloadObject(string url)
        {
            using (WebClient client = new WebClient())
            {
                byte[] fileData = client.DownloadData(url);
                File.WriteAllBytes(downloadPath, fileData);
            }
        }

        private static async Task<string> GeneratePreSignedURL(string urlFor)
        {
            // HttpResponseMessage response = await client.GetAsync(@$"https://ximr5q0l96.execute-api.us-east-1.amazonaws.com/Prod?body={urlFor}");
            string url = string.Empty;
            if (urlFor == "download")
            {
                url = @"https://ni939svok6.execute-api.us-east-1.amazonaws.com/Prod/client-download";
            }
            else
            {
                url = @"https://ni939svok6.execute-api.us-east-1.amazonaws.com/Prod/client-upload?filename=upload.xml";
            }
            HttpResponseMessage response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var preSigned = JsonConvert.DeserializeObject<PreSignedURL>(await response.Content.ReadAsStringAsync());
                return preSigned.Url;
            }
            return "";
        }
    }

    public class PreSignedURL
    {
        public string Url { get; set; }
        public string For { get; set; }
    }
}
