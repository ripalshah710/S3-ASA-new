using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System;
using System.IO;
using System.Text.Json;

namespace S3_ASA
{
    public class SecretManager
    {
        /*
         *	Use this code snippet in your app.
         *	If you need more information about configurations or implementing the sample code, visit the AWS docs:
         *	https://aws.amazon.com/developers/getting-started/net/
         *	
         *	Make sure to include the following packages in your code.
         *	
         *	using System;
         *	using System.IO;
         * 
         *	using Amazon;
         *	using Amazon.SecretsManager;
         *	using Amazon.SecretsManager.Model;
         *
         */

        /*
         * AWSSDK.SecretsManager version="3.3.0" targetFramework="net45"
         */
        private static string GetSecret()
        {
            string secretName = "Region4DBConnection";
            string region = "us-east-1";
            string secret = "";

            MemoryStream memoryStream = new MemoryStream();

            IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            GetSecretValueRequest request = new GetSecretValueRequest();
            request.SecretId = secretName;
            request.VersionStage = "AWSCURRENT"; // VersionStage defaults to AWSCURRENT if unspecified.

            GetSecretValueResponse response = null;

            // In this sample we only handle the specific exceptions for the 'GetSecretValue' API.
            // See https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
            // We rethrow the exception by default.

            try
            {
                response = client.GetSecretValueAsync(request).Result;
            }
            catch (Exception ex)
            {
                // More than one of the above exceptions were triggered.
                // Deal with the exception here, and/or rethrow at your discretion.
                throw;
            }

            // Decrypts secret using the associated KMS key.
            // Depending on whether the secret is a string or binary, one of these fields will be populated.
            if (response.SecretString != null)
            {
                secret = response.SecretString;
            }
            else
            {
                memoryStream = response.SecretBinary;
                StreamReader reader = new StreamReader(memoryStream);
                secret = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(reader.ReadToEnd()));
            }
            return secret;
        }

        public static string GetConnectionString()
        {
            var secretResponse = JsonSerializer.Deserialize<SecretResponse>(GetSecret());
            return $"Server={secretResponse.host};Database={secretResponse.dbname};user id={secretResponse.username};password={secretResponse.password};";
        }
    }

    public class SecretResponse
    { 
        public string username { get; set; }
        public string password { get; set; }
        public string engine { get; set; }
        public string host { get; set; }
        public string port { get; set; }
        public string dbname { get; set; }
    }
}
