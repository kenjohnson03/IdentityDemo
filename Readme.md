# Identity Demo

Follow the instructions here:
https://learn.microsoft.com/en-us/azure/active-directory/develop/custom-extension-get-started?tabs=azure-portal

## Setup AzureADQuery Function
Environment Variables
```
AzureApp:ClientSecret: "SECRET_GOES_HERE"
AzureAd:ClientId: "This is a guid found on the App Registration Overview for the app"
AzureAd:TenantId: "This is a guid found on the Overview blade under Identity in the Microsoft Entra admin center"
AzureEnvironment: "AzurePublicCloud" or "AzureUSGovernment"
```

## Setup LDAPQuery Function
Environment Variables
```
LDAP_Connection: "ldap://ds.example.com:389/dc=example,dc=com"
```

## Setup IdentityDemo Blazor App

Environment Variables
```
"AzureAd": {
    "Instance": "https://login.microsoftonline.com/" or "https://login.microsoftonline.us/",
    "Domain": "This is a guid found on the Overview blade under Identity in the Microsoft Entra admin center similar to something.onmicrosoft.com",
    "TenantId": "This is a guid found on the Overview blade under Identity in the Microsoft Entra admin center",
    "ClientId": "This is a guid found on the App Registration Overview for the app",
    "CallbackPath": "/signin-oidc"
  }
```