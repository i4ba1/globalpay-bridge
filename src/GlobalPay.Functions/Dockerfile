FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./

COPY src/GlobalPay.Domain/GlobalPay.Domain.csproj src/GlobalPay.Domain/
COPY src/GlobalPay.Contracts/GlobalPay.Contracts.csproj src/GlobalPay.Contracts/
COPY src/GlobalPay.Application/GlobalPay.Application.csproj src/GlobalPay.Application/
COPY src/GlobalPay.Infrastructure/GlobalPay.Infrastructure.csproj src/GlobalPay.Infrastructure/
COPY src/GlobalPay.Functions/GlobalPay.Functions.csproj src/GlobalPay.Functions/

RUN dotnet restore src/GlobalPay.Functions/GlobalPay.Functions.csproj

COPY src/ src/

RUN dotnet publish src/GlobalPay.Functions/GlobalPay.Functions.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS final

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

COPY --from=build /app/publish /home/site/wwwroot
