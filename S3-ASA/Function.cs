using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using System.Text.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.APIGatewayEvents;
using System.Data.SqlClient;
using System.Data;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon;
using System.Text;
using System.IO;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace S3_ASA
{
    public class Functions
    {
        public IAmazonS3 S3Client;
        private const double timeoutDuration = 12;

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
            RegionEndpoint bucketRegion = RegionEndpoint.USEast1;
            S3Client = new AmazonS3Client(bucketRegion);
        }


        /// <summary>
        /// A Lambda function to respond to HTTP Get methods from API Gateway
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The API Gateway response.</returns>
        public async Task<string> SessionListHandler(CloudWatchEvent<dynamic> evnt, ILambdaContext context)
        {
            context.Logger.LogLine(JsonSerializer.Serialize(evnt));
            await PerformOperations(context);
            return "Done";
            //"Server=R04HOUSQL82A\ESCDB;Database=tx_r8;Trusted_Connection=True;MultipleActiveResultSets=true;MultiSubnetFailover=True;"
        }

        public async Task<APIGatewayProxyResponse> UploadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            await PerformOperations(context);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = @"File Uploaded to S3",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/json" } }
            };
            return response; 
        }

        public async Task<APIGatewayProxyResponse> DownloadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            //await PerformOperations(context);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = await ReadFileFromS3(context),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/xml" } }
            };
            return response;
        }

        public APIGatewayProxyResponse ClientUploadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            var url = GeneratePreSignedURL(timeoutDuration, HttpVerb.PUT);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = @"{Url:'" + url + "', for:'download'}",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/json" } }
            };
            return response;
        }

        public  APIGatewayProxyResponse ClientDownloadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            var url = GeneratePreSignedURL(timeoutDuration, HttpVerb.GET);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = @"{Url:'" + url + "', for:'upload'}",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/json" } }
            };
            return response;
        }

        private async Task PerformOperations(ILambdaContext context)
        {
            using (SqlCommand command = new SqlCommand("[/sysmail/tx_r16/sessionlist]", new SqlConnection(ConnectionString)))
            {
                command.CommandType = CommandType.StoredProcedure;
                try
                {
                    DataSet dataSet = new DataSet("courses");
                    command.Connection.Open();
                    new SqlDataAdapter(command).Fill(dataSet, "session");
                    StringBuilder sb = new StringBuilder();
                    StringWriter sw = new StringWriter(sb);
                    dataSet.WriteXml(sw);
                    await PushFiletoS3(sw, context);
                }
                catch (Exception exception)
                {
                    context.Logger.LogLine("Error saving xml to S3" + exception.Message + "<br>InnerException<br>" + exception.InnerException + "<br>source:<br>" + exception.Source);
                }
                finally
                {
                    if (command.Connection.State != ConnectionState.Closed)
                    {
                        command.Connection.Close();
                    }
                }
            }
        }

        private async Task<bool> PushFiletoS3(StringWriter content, ILambdaContext context)
        {
            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = BucketName,
                    Key = $"{Path}/{FileName}",
                    ContentBody = content.ToString()
                };
                var response = await S3Client.PutObjectAsync(request);
                context.Logger.LogLine($"New TXT {Path} file created - and pushed to S3 ");
                return true;
            }
            catch (Exception ex)
            {
                context.Logger.LogLine("Exception in Push S3Object:" + ex.Message);
                return false;
            }
        }

        private async Task<string> ReadFileFromS3(ILambdaContext context)
        {
            string responseBody = "";
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = BucketName,
                    Key = $"{Path}/{FileName}"
                };
                using (GetObjectResponse response = await S3Client.GetObjectAsync(request))
                using (Stream responseStream = response.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string title = response.Metadata["x-amz-meta-title"]; // Assume you have "title" as medata added to the object.
                    string contentType = response.Headers["Content-Type"];
                    Console.WriteLine("Object metadata, Title: {0}", title);
                    Console.WriteLine("Content type: {0}", contentType);

                    responseBody = reader.ReadToEnd(); // Now you process the response body.
                }
            }
            catch (AmazonS3Exception e)
            {
                // If bucket or object does not exist
                Console.WriteLine("Error encountered ***. Message:'{0}' when reading object", e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown encountered on server. Message:'{0}' when reading object", e.Message);
            }
            return responseBody;
        }

        private  string GeneratePreSignedURL(double duration, Amazon.S3.HttpVerb httpVerb)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = BucketName,
                Key = $"{Path}/{FileName}",
                Verb = httpVerb,
                Expires = DateTime.UtcNow.AddHours(duration)
            };
            //request.Headers["x-amz-acl"] = "public-read";

            string url = S3Client.GetPreSignedURL(request);
            return url;
        }

        private string ConnectionString
        {
            get
            {
                return "Data Source=172.17.34.228;Initial Catalog=tx_r16;user id=devteam;password=H0ll1ster~"; // Environment.GetEnvironmentVariable("DB_CONNECTION");
            }
        }

        private string BucketName
        {
            get
            {
                return "ftp-operation";// Environment.GetEnvironmentVariable("BUCKET_NAME");
            }
        }

        private string FileName
        {
            get
            {
                return "session_updated.xml";//Environment.GetEnvironmentVariable("FILE_NAME");
            }
        }

        private string Path
        {
            get
            {
                return "DailySessionList";// Environment.GetEnvironmentVariable("PATH_NAME");
            }
        }
    }
}
