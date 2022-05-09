using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S3_ASA
{
    public class Authorizer
    {
        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Authorizer()
        {
        }

        /// <summary>
        /// A Lambda function to respond to HTTP Get methods from API Gateway
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The API Gateway response.</returns>
        public APIGatewayCustomAuthorizerResponse Get(APIGatewayCustomAuthorizerRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");
            var item = string.Empty;
            try
            {
                item = request.Headers.Where(x => String.Equals(x.Key, "userName", StringComparison.InvariantCultureIgnoreCase))
                    .Select(p => p.Value).Single();
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"Exception occured when reading userName header : {e}");
                item = "";
            }
            string userName = item.ToString();
            var item1 = string.Empty;
            try
            {
                item1 = request.Headers.Where(x => String.Equals(x.Key, "passWord", StringComparison.InvariantCultureIgnoreCase))
                    .Select(p => p.Value).Single();
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"Exception occured when reading passWord header : {e}");
                item1 = "";
            }
            string passWord = item1.ToString();
            #region checkHeaders
            if (userName.Equals("hardik") && passWord.Equals("pass"))
            {
                APIGatewayCustomAuthorizerPolicy policy = new APIGatewayCustomAuthorizerPolicy
                {
                    Version = "2012-10-17",
                    Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>()
                };
                policy.Statement.Add(new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                {
                    Action = new HashSet<string>(new string[] { "execute-api:Invoke" }),
                    Effect = "Allow",
                    Resource = new HashSet<string>(new string[] { request.MethodArn })
                });

                APIGatewayCustomAuthorizerContextOutput contextOutput = new APIGatewayCustomAuthorizerContextOutput();
                contextOutput["User"] = "User";
                contextOutput["Path"] = request.MethodArn;
                return new APIGatewayCustomAuthorizerResponse
                {
                    PrincipalID = "User",
                    Context = contextOutput,
                    PolicyDocument = policy
                };
            }

            #endregion

            #region response
            APIGatewayCustomAuthorizerPolicy newPolicy = new APIGatewayCustomAuthorizerPolicy
            {
                Version = "2012-10-17",
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>()
            };
            newPolicy.Statement.Add(new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
            {
                Action = new HashSet<string>(new string[] { "execute-api:Invoke" }),
                Effect = "Deny",
                Resource = new HashSet<string>(new string[] { request.MethodArn })
            });

            APIGatewayCustomAuthorizerContextOutput newContextOutput = new APIGatewayCustomAuthorizerContextOutput();
            newContextOutput["User"] = "User";
            newContextOutput["Path"] = request.MethodArn;
            return new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = "User",
                Context = newContextOutput,
                PolicyDocument = newPolicy
            };
            #endregion

        }
    }
}
