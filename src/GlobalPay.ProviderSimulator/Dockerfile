FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./

COPY src/GlobalPay.Contracts/GlobalPay.Contracts.csproj src/GlobalPay.Contracts/
COPY src/GlobalPay.ProviderSimulator/GlobalPay.ProviderSimulator.csproj src/GlobalPay.ProviderSimulator/

RUN dotnet restore src/GlobalPay.ProviderSimulator/GlobalPay.ProviderSimulator.csproj

COPY src/ src/

RUN dotnet publish src/GlobalPay.ProviderSimulator/GlobalPay.ProviderSimulator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GlobalPay.ProviderSimulator.dll"]
