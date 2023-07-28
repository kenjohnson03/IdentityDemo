# Identity Demo

Follow the instructions here:
https://learn.microsoft.com/en-us/azure/active-directory/develop/custom-extension-get-started?tabs=azure-portal

## Setup AzureADQuery
Envitonment Variables
```
AzureApp:ClientSecret: "SECRET_GOES_HERE"
AzureAd:ClientId: "This is a guid found on the App Registration Overview for the app"
AzureAd:TenantId: "This is a guid found on the Overview blade under Identity in the Microsoft Entra Portal"
AzureEnvironment: "AzurePublicCloud" or "AzureUSGovernment"
```

## Setup LDAPQuery
Envitonment Variables
```
LDAP_Connection: "ldap://ds.example.com:389/dc=example,dc=com"
```

