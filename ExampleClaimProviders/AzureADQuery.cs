using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ExampleClaimProviders.Models;
using System.Reflection.PortableExecutable;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace ExampleClaimProviders
{
    public static class AzureADQuery
    {
        private static ILogger _logger;

        [FunctionName("AzureADQuery")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            _logger = log;
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation(requestBody);
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            // Read the correlation ID from the Azure AD  request
            /* Example request from Azure AD:
            {
                "type": "microsoft.graph.authenticationEvent.tokenIssuanceStart",
                "source": "/tenants/<Your tenant GUID>/applications/<Your Test Application App Id>",
                "data": {
                    "@odata.type": "microsoft.graph.onTokenIssuanceStartCalloutData",
                    "tenantId": "<Your tenant GUID>",
                    "authenticationEventListenerId": "<GUID>",
                    "customAuthenticationExtensionId": "<Your custom extension ID>",
                    "authenticationContext": {
                        "correlationId": "fcef74ef-29ea-42ca-b150-8f45c8f31ee6",
                        "client": {
                                        "ip": "127.0.0.1",
                            "locale": "en-us",
                            "market": "en-us"
                        },
                        "protocol": "OAUTH2.0",
                        "clientServicePrincipal": {
                            "id": "<Your Test Applications servicePrincipal objectId>",
                            "appId": "<Your Test Application App Id>",
                            "appDisplayName": "My Test application",
                            "displayName": "My Test application"
                        },
                        "resourceServicePrincipal": {
                            "id": "<Your Test Applications servicePrincipal objectId>",
                            "appId": "<Your Test Application App Id>",
                            "appDisplayName": "My Test application",
                            "displayName": "My Test application"
                        },
                        "user": {
                            "createdDateTime": "2016-03-01T15:23:40Z",
                            "displayName": "John Smith",
                            "givenName": "John",
                            "id": "90847c2a-e29d-4d2f-9f54-c5b4d3f26471",
                            "mail": "john@contoso.com",
                            "preferredLanguage": "en-us",
                            "surname": "Smith",
                            "userPrincipalName": "john@contoso.com",
                            "userType": "Member"
                        }
                    }
                }
            }
            */
            string correlationId = data?.data.authenticationContext.correlationId;
            string oid = data?.data.authenticationContext.user.id;
            if(string.IsNullOrEmpty(oid))
            {
                _logger.LogError("OID is null or empty");
                return new BadRequestObjectResult("OID is null or empty");
            }

            // Claims to return to Azure AD
            ResponseContent r = new ResponseContent();
            r.data.actions[0].claims.CorrelationId = correlationId;
            r.data.actions[0].claims.ApiVersion = "1.0.0";
            r.data.actions[0].claims.UPN = await GetOnPremUPNFromAzureAD(oid);
            r.data.actions[0].claims.CustomRoles.Add("Writer");
            r.data.actions[0].claims.CustomRoles.Add("Editor");
            return new OkObjectResult(r);
        }

        private static async Task<string> GetOnPremUPNFromAzureAD(string userId)
        {
            string upn = string.Empty;
            MethodBase method = System.Reflection.MethodBase.GetCurrentMethod();
            string methodName = method.Name;
            string className = method.ReflectedType.Name;
            string fullMethodName = className + "." + methodName;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError($"{fullMethodName} Error: User name is null or empty. userId: {userId}");
                return upn;
            }
            

            var TargetCloud = Environment.GetEnvironmentVariable("AzureEnvironment", EnvironmentVariableTarget.Process);

            string tokenUri = "";
            if (TargetCloud == "AzurePublicCloud")
            {
                tokenUri = "https://graph.microsoft.com";
            }
            else
            {
                tokenUri = "https://graph.microsoft.us";
            }


            try
            {

                var graphAccessToken = await GetAccessTokenAsync(tokenUri);
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", graphAccessToken);


                string azureADRequestUri = $"{tokenUri}/v1.0/users/{userId}?$select=onPremisesUserPrincipalName,onPremisesSamAccountName";
                var azureADRequest = new HttpRequestMessage(HttpMethod.Get, azureADRequestUri);
                azureADRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphAccessToken);
                HttpResponseMessage azureADResponse = await httpClient.SendAsync(azureADRequest);
                azureADResponse.EnsureSuccessStatusCode();
                string azureADResponseContent = await azureADResponse.Content.ReadAsStringAsync();

                dynamic data = JsonConvert.DeserializeObject(azureADResponseContent);
                upn = data.onPremisesUserPrincipalName;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{fullMethodName} Error: {ex.Message}");
            }
            return upn;
        }

        private static async Task<String> GetAccessTokenAsync(string uri)
        {
            MethodBase method = System.Reflection.MethodBase.GetCurrentMethod();
            string methodName = method.Name;
            string className = method.ReflectedType.Name;
            string fullMethodName = className + "." + methodName;

            var AppSecret = Environment.GetEnvironmentVariable("AzureApp:ClientSecret", EnvironmentVariableTarget.Process);
            var AppId = Environment.GetEnvironmentVariable("AzureAd:ClientId", EnvironmentVariableTarget.Process);
            var TenantId = Environment.GetEnvironmentVariable("AzureAd:TenantId", EnvironmentVariableTarget.Process);
            var TargetCloud = Environment.GetEnvironmentVariable("AzureEnvironment", EnvironmentVariableTarget.Process);

            if (String.IsNullOrEmpty(AppSecret) || String.IsNullOrEmpty(AppId) || String.IsNullOrEmpty(TenantId))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{fullMethodName} Error: Missing required environment variables. Please check the following environment variables are set:");
                sb.Append(String.IsNullOrEmpty(AppSecret) ? "AzureApp:ClientSecret\n" : "");
                sb.Append(String.IsNullOrEmpty(AppId) ? "AzureAd:ClientId\n" : "");
                sb.Append(String.IsNullOrEmpty(TenantId) ? "AzureAd:TenantId\n" : "");
                _logger.LogError(sb.ToString());
                return null;
            }

            string tokenUri = "";
            if (TargetCloud == "AzurePublicCloud")
            {
                tokenUri = $"https://login.microsoftonline.com/{TenantId}/oauth2/token";
            }
            else
            {
                // TODO update URI
                tokenUri = $"https://login.microsoftonline.us/{TenantId}/oauth2/token";
            }

            // Get token for Log Analytics

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUri);
            tokenRequest.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", AppId),
                new KeyValuePair<string, string>("client_secret", AppSecret),
                new KeyValuePair<string, string>("resource", uri)
            });

            try
            {
                var httpClient = new HttpClient();
                var tokenResponse = await httpClient.SendAsync(tokenRequest);
                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = JsonConvert.DeserializeObject<dynamic>(tokenContent);
                return tokenData.access_token;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{fullMethodName} Error: getting access token for URI {tokenUri}: {ex.Message}");
                return null;
            }
        }

    }
}
