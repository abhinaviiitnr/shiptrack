# Multi-stage Dockerfile for Gateway (YARP)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY Directory.Build.props Directory.Packages.props ./
COPY src/gateway/Gateway/Gateway.csproj ./src/gateway/Gateway/
RUN dotnet restore src/gateway/Gateway/Gateway.csproj

COPY src/gateway/Gateway/ ./src/gateway/Gateway/
RUN dotnet publish src/gateway/Gateway/Gateway.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
USER appuser
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gateway.dll"]
