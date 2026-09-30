using '../main.bicep'

param environment = 'dev'
param baseName = 'globalpay'
param sqlAdminLogin = 'globalpayadmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param oidcAuthority = 'https://login.microsoftonline.com/REPLACE_TENANT_ID/v2.0'
param oidcAudience = 'globalpay-api'
param paymentProviderBaseAddress = 'https://REPLACE_PROVIDER_HOST'
