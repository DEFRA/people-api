# syntax=docker/dockerfile:1
# Builds defra-people-api for Azure Container Apps / App Service (Linux) / AKS.
# Base image pins: pin to a specific tag (not ":latest") so builds are reproducible; bump deliberately.
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra

FROM $DOTNET_SDK_IMAGE AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the project file first so `dotnet restore` is cached as its own layer and only reruns when
# dependencies change, not on every source edit.
COPY defra-people-api/defra-people-api.csproj defra-people-api/
RUN dotnet restore defra-people-api/defra-people-api.csproj

COPY defra-people-api/ defra-people-api/
WORKDIR /src/defra-people-api
# No --no-restore: the separate restore layer above can leave publish unable to see
# transitively-restored Azure SDK packages, so let publish restore again (fast no-op if unchanged).
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false

# The app persists the encrypted admin/master keys back into appsettings.json and loads API keys
# from a local "Keys" directory at startup (see EncryptedConfigService/ApiKeyCacheService), so the
# directory must exist before the non-root chown'd copy below (the chiseled runtime stage has no
# shell, so this can't be created with RUN there).
RUN mkdir -p /app/publish/Keys

# Chiseled "-extra" runtime: no shell/package manager (smaller attack surface, fewer CVEs) but keeps
# ICU + tzdata, which EF Core/SqlClient need for collation/culture-aware operations.
FROM $DOTNET_RUNTIME_IMAGE AS final
WORKDIR /app
COPY --chown=$APP_UID:$APP_UID --from=build /app/publish .

# ASPNETCORE_HTTP_PORTS (not ASPNETCORE_URLS) is the Microsoft-recommended way to set the listen port
# since .NET 8; the ingress/platform routes traffic here and it backs the /health/live and
# /health/ready probes (see Endpoints/HealthChecks/Healthcheck.cs).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# NOTE: the Keys directory and appsettings.json rewrite above are per-instance, on-disk state that
# does not survive redeploys or scale-out to multiple replicas; see docs/Operations-Notes.md for the
# recommended durable-storage follow-up.

# Chiseled images already default to a non-root user, but setting it explicitly documents the intent
# and matches the Microsoft .NET container samples. $APP_UID is defined by the base image.
USER $APP_UID

# OCI metadata for image provenance/supply-chain scanning; no functional effect.
LABEL org.opencontainers.image.title="defra-people-api" \
      org.opencontainers.image.description="Defra People API - Microsoft Graph user lookup service" \
      org.opencontainers.image.vendor="DEFRA" \
      org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra"

ENTRYPOINT ["dotnet", "defra-people-api.dll"]
