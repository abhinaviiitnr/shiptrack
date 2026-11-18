# Multi-stage Dockerfile for Orders.Api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY Directory.Build.props Directory.Packages.props ./
COPY src/services/Orders.Api/Orders.Api.csproj ./src/services/Orders.Api/
RUN dotnet restore src/services/Orders.Api/Orders.Api.csproj

COPY src/services/Orders.Api/ ./src/services/Orders.Api/
RUN dotnet publish src/services/Orders.Api/Orders.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
USER appuser
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
