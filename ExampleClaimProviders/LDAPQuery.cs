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
using System.Reflection;
using System.Text;

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
            // Validate environment variables are set
            MethodBase method = System.Reflection.MethodBase.GetCurrentMethod();
            string methodName = method.Name;
            string className = method.ReflectedType.Name;
            string fullMethodName = className + "." + methodName;

            var LDAPPath = Environment.GetEnvironmentVariable("LDAP_Path", EnvironmentVariableTarget.Process);
            var LDAPUsername = Environment.GetEnvironmentVariable("LDAP_Username", EnvironmentVariableTarget.Process);
            var LDAPPassword = Environment.GetEnvironmentVariable("LDAP_Password", EnvironmentVariableTarget.Process);

            if (String.IsNullOrEmpty(LDAPPath) || String.IsNullOrEmpty(LDAPUsername) || String.IsNullOrEmpty(LDAPPassword))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{fullMethodName} Error: Missing required environment variables. Please check the following environment variables are set:");
                sb.Append(String.IsNullOrEmpty(LDAPPath) ? "LDAP_Path\n" : "");
                sb.Append(String.IsNullOrEmpty(LDAPUsername) ? "LDAP_Username\n" : "");
                sb.Append(String.IsNullOrEmpty(LDAPPassword) ? "LDAP_Password\n" : "");
                _logger.LogError(sb.ToString());
                return null;
            }


            string upn = string.Empty;
            if(string.IsNullOrEmpty(userName))
            {
                _logger.LogError($"{fullMethodName} Error: userName is null or empty.");
                return upn;
            }
  
            try
            {
                System.DirectoryServices.DirectoryEntry entry = new DirectoryEntry(LDAPPath, LDAPUsername, LDAPPassword);
                DirectorySearcher searcher = new DirectorySearcher(entry);
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
                _logger.LogError($"{fullMethodName} Error:\n{ex.Message}");
            }            

            return upn;
        }
    }
}
