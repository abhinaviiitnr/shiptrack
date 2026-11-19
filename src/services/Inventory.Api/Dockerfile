# Multi-stage Dockerfile for Inventory.Api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY Directory.Build.props Directory.Packages.props ./
COPY src/services/Inventory.Api/Inventory.Api.csproj ./src/services/Inventory.Api/
RUN dotnet restore src/services/Inventory.Api/Inventory.Api.csproj

COPY src/services/Inventory.Api/ ./src/services/Inventory.Api/
RUN dotnet publish src/services/Inventory.Api/Inventory.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
