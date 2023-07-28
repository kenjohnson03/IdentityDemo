using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.DirectoryServices;
using ExampleClaimProviders.Models;

namespace ExampleClaimProviders
{
    public static class LDAPQuery
    {
        private static ILogger _logger;

        [FunctionName("LDAPQuery")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            _logger = log;
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            // Read the correlation ID from the Azure AD  request    
            string correlationId = data?.data.authenticationContext.correlationId;
            string upn = data?.data.authenticationContext.user.userPrincipalName;


            // Claims to return to Azure AD
            ResponseContent r = new ResponseContent();
            r.data.actions[0].claims.CorrelationId = correlationId;
            r.data.actions[0].claims.ApiVersion = "1.0.0";
            r.data.actions[0].claims.UPN = GetUPNFromLDAP(upn);
            r.data.actions[0].claims.CustomRoles.Add("Writer");
            r.data.actions[0].claims.CustomRoles.Add("Editor");
            return new OkObjectResult(r);
        }

        private static string GetUPNFromLDAP(string userName)
        {
            string upn = string.Empty;
            if(string.IsNullOrEmpty(userName))
            {
                _logger.LogError("userName is null or empty");
                return upn;
            }
            var connectionString = Environment.GetEnvironmentVariable("LDAP_Connection", EnvironmentVariableTarget.Process);
            if(string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("LDAP_Connection environment variable not set");
                return upn;
            }            
            try
            {
                System.DirectoryServices.DirectoryEntry entry = new(connectionString);
                DirectorySearcher searcher = new(entry);
                searcher.Filter = $"(&(objectClass=user)(mail={userName}))";
                SearchResultCollection results = searcher.FindAll();
                foreach(SearchResult result in results)
                {
                    DirectoryEntry de = result.GetDirectoryEntry();
                    if (!string.IsNullOrEmpty(de.Properties["userPrincipalName"].Value.ToString()))
                    {
                        upn = de.Properties["userPrincipalName"].Value.ToString();
                        break;
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            

            return upn;
        }
    }
}
