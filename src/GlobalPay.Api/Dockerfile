FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./

COPY src/GlobalPay.Domain/GlobalPay.Domain.csproj src/GlobalPay.Domain/
COPY src/GlobalPay.Contracts/GlobalPay.Contracts.csproj src/GlobalPay.Contracts/
COPY src/GlobalPay.Application/GlobalPay.Application.csproj src/GlobalPay.Application/
COPY src/GlobalPay.Infrastructure/GlobalPay.Infrastructure.csproj src/GlobalPay.Infrastructure/
COPY src/GlobalPay.Api/GlobalPay.Api.csproj src/GlobalPay.Api/

RUN dotnet restore src/GlobalPay.Api/GlobalPay.Api.csproj

COPY src/ src/

RUN dotnet publish src/GlobalPay.Api/GlobalPay.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GlobalPay.Api.dll"]
