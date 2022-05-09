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
using FluentFTP;
using System.Threading;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace S3_ASA
{
    public class Functions
    {
        public IAmazonS3 S3Client;
        private const double timeoutDuration = 12;

        private enum UploadType
        {
            FTP,
            S3
        }

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
            RegionEndpoint bucketRegion = RegionEndpoint.USEast1;
            S3Client = new AmazonS3Client(bucketRegion);
            ConnectionString = SecretManager.GetConnectionString();
        }


        /// <summary>
        /// Scheduled based Lambda where It trigger for clouldwatch event
        /// </summary>
        /// <param name="evnt">ClouldWatch Event</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public async Task<string> SessionListHandler(CloudWatchEvent<dynamic> evnt, ILambdaContext context)
        {
            context.Logger.LogLine(JsonSerializer.Serialize(evnt));
            await GetDataFromDBPush(context, UploadType.S3);
            return "Done";
            //"Server=R04HOUSQL82A\ESCDB;Database=tx_r8;Trusted_Connection=True;MultipleActiveResultSets=true;MultiSubnetFailover=True;"
        }

        /// <summary>
        /// On Demand upload to S3
        /// </summary>
        /// <param name="request">API Gateway Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> UploadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            
            await GetDataFromDBPush(context, UploadType.S3);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = @"File Uploaded to S3",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/json" } }
            };
            return response;
        }

        /// <summary>
        /// On Demand download from S3
        /// </summary>
        /// <param name="request">API Gateway Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> DownloadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            //await PerformOperations(context);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = await ReadFileFromS3(string.Empty, context),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/xml" } }
            };
            return response;
        }

        /// <summary>
        /// On Cliet site upload to S3 using Presigned URL
        /// </summary>
        /// <param name="request">API Gateway Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public APIGatewayProxyResponse ClientUploadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            string fileName = request.QueryStringParameters["filename"].ToLower();
            string contentType = request.QueryStringParameters["contenttype"].ToLower();
            var url = GeneratePreSignedURL(timeoutDuration, fileName, HttpVerb.PUT, contentType);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = @"{Url:'" + url + "', for:'upload'}",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/json" } }
            };
            return response;
        }

        /// <summary>
        /// On Cliet site download from S3 using Presigned URL
        /// </summary>
        /// <param name="request">API Gateway Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public APIGatewayProxyResponse ClientDownloadS3(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            var url = GeneratePreSignedURL(timeoutDuration, string.Empty, HttpVerb.GET, string.Empty);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = @"{Url:'" + url + "', for:'download'}",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/json" } }
            };
            return response;
        }

        /// <summary>
        /// S3 Client HTML Page
        /// </summary>
        /// <param name="request">API Gateway Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> S3ClientUrl(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            var html = "<html><h1>SUPER HTML</h1></html>";
            html = await ReadFileFromS3("S3-Client.html", context);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = html,
                Headers = new Dictionary<string, string> { { "Content-Type", "text/html" } }
            };
            return response;
        }

        /// <summary>
        /// On Demand upload to FTP
        /// </summary>
        /// <param name="request">API Gateway Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> UploadFTP(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            await GetDataFromDBPush(context, UploadType.FTP);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = @"File Uploaded to FTP",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/json" } }
            };
            return response;
        }

        /// <summary>
        /// On Demand download from FTP
        /// </summary>
        /// <param name="request">API Gateway Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> DownloadFTP(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            //await PerformOperations(context);
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = await ReadFileFromFTP(string.Empty, context),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/xml" } }
            };
            return response;
        }

        private async Task GetDataFromDBPush(ILambdaContext context, UploadType uploadType)
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
                    if (uploadType == UploadType.FTP)
                    {
                        await PushFiletoFTP(sw, context);
                    }
                    else if (uploadType == UploadType.S3)
                    {
                        await PushFiletoS3(sw, context);
                    }
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

        private async Task<string> ReadFileFromS3(string fileName, ILambdaContext context)
        {
            string responseBody = "";
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = FileName;
                }
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = BucketName,
                    Key = $"{Path}/{fileName}"
                };
                using (GetObjectResponse response = await S3Client.GetObjectAsync(request))
                using (Stream responseStream = response.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string title = response.Metadata["x-amz-meta-title"]; // Assume you have "title" as medata added to the object.
                    string contentType = response.Headers["Content-Type"];
                    context.Logger.LogLine($"Object metadata, Title: {title}");
                    context.Logger.LogLine($"Content type: {contentType}");

                    responseBody = reader.ReadToEnd(); // Now you process the response body.
                }
            }
            catch (AmazonS3Exception e)
            {
                // If bucket or object does not exist
                context.Logger.LogLine($"Error encountered ***. Message:'{e.Message}' when reading object");
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"Unknown encountered on server. Message:'{e.Message}' when reading object");
            }
            return responseBody;
        }

        private async Task<bool> PushFiletoFTP(StringWriter content, ILambdaContext context)
        {
            try
            {
                string ftpUrl = $"ftps://{FtpServer}:{FtpPort}/{FtpFilePath}";
                NetworkCredential networkCredential = new NetworkCredential(FtpUserName, FtpPassWord);
                var token = new CancellationToken();
                using (var client = new FluentFTP.FtpClient(ftpUrl, networkCredential))
                {
                    client.EncryptionMode = FtpEncryptionMode.Explicit;
                    client.ValidateAnyCertificate = true;
                    await client.ConnectAsync(token);
                    await client.UploadAsync(Encoding.ASCII.GetBytes(content.ToString()), FtpFileName);
                }
                context.Logger.LogLine($"New TXT {Path} file created - and pushed to FTP ");
                return true;
            }
            catch (Exception ex)
            {
                context.Logger.LogLine("Exception in Push FTP:" + ex.Message);
                return false;
            }
        }

        private async Task<string> ReadFileFromFTP(string fileName, ILambdaContext context)
        {
            string responseBody = "";
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = FileName;
                }
                string ftpUrl = $"ftps://{FtpServer}:{FtpPort}/{FtpFilePath}";
                NetworkCredential networkCredential = new NetworkCredential(FtpUserName, FtpPassWord);
                var token = new CancellationToken();
                using (var client = new FluentFTP.FtpClient(ftpUrl, networkCredential))
                {
                    client.EncryptionMode = FtpEncryptionMode.Explicit;
                    client.ValidateAnyCertificate = true;
                    await client.ConnectAsync(token);
                    Stream responseStream = new System.IO.MemoryStream();
                    var fileStatus = await client.DownloadAsync(responseStream, FtpFileName, restartPosition: 0);
                    if (fileStatus)
                    {
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            responseBody = reader.ReadToEnd(); // Now you process the response body.
                        }
                    }
                    else
                    {
                        context.Logger.LogLine("Download from FTP return false");
                    }
                    //byte[] fileData = await client.DownloadAsync(FtpFileName, restartPosition:0);
                    //responseBody = System.Text.Encoding.Default.GetString(fileData);
                }
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"Unknown encountered on server. Message:'{0}' when reading object from FTP {e.Message}");
            }
            return responseBody;
        }

        private string GeneratePreSignedURL(double duration, string fileName, Amazon.S3.HttpVerb httpVerb, string contentType)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = FileName;
            }
            var request = new GetPreSignedUrlRequest
            {
                BucketName = BucketName,
                Key = $"{Path}/{fileName}",
                Verb = httpVerb,
                Expires = DateTime.UtcNow.AddHours(duration),
                ContentType = contentType

            };
            //request.Headers["x-amz-acl"] = "public-read";

            string url = S3Client.GetPreSignedURL(request);
            return url;
        }

        private string ConnectionString { get; set; }
        //get
        //{
        //    return "Data Source=172.17.34.228;Initial Catalog=tx_r16;user id=devteam;password=H0ll1ster~"; // Environment.GetEnvironmentVariable("DB_CONNECTION");
        //}

        private string BucketName
        {
            get
            {
                return Environment.GetEnvironmentVariable("BUCKET_NAME") ?? "ftp-operation";
            }
        }

        private string FileName
        {
            get
            {
                return Environment.GetEnvironmentVariable("FILE_NAME") ?? "session_updated.xml";
            }
        }

        private string FtpServer
        {
            get
            {
                return Environment.GetEnvironmentVariable("FTP_SERVER") ?? "session_updated.xml";
            }
        }

        private string FtpUserName
        {
            get
            {
                return Environment.GetEnvironmentVariable("FTP_USER_NAME") ?? "session_updated.xml";
            }
        }

        private string FtpPassWord
        {
            get
            {
                return Environment.GetEnvironmentVariable("FTP_PASSWORD") ?? "session_updated.xml";
            }
        }

        private string FtpPort
        {
            get
            {
                return Environment.GetEnvironmentVariable("FTP_PORT") ?? "session_updated.xml";
            }
        }

        private string FtpFilePath
        {
            get
            {
                return Environment.GetEnvironmentVariable("FTP_FILE_PATH") ?? "session_updated.xml";
            }
        }

        private string FtpFileName
        {
            get
            {
                return Environment.GetEnvironmentVariable("FTP_FILE_NAME") ?? "session_updated.xml";
            }
        }

        private string Path
        {
            get
            {
                return Environment.GetEnvironmentVariable("PATH_NAME") ?? "DailySessionList";
            }
        }
    }
}
