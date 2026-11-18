# Multi-stage build: keeps the final image small and secure.
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files first to leverage Docker layer caching.
# If only source changes, the restore layer is reused.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/services/Identity.Api/Identity.Api.csproj ./src/services/Identity.Api/
RUN dotnet restore src/services/Identity.Api/Identity.Api.csproj

# Copy source and build in Release mode.
COPY src/services/Identity.Api/ ./src/services/Identity.Api/
RUN dotnet publish src/services/Identity.Api/Identity.Api.csproj \
    -c Release -o /app/publish --no-restore

# Stage 2: Runtime — much smaller than the SDK image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# WHY: Run as non-root to limit blast radius if the container is compromised.
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
