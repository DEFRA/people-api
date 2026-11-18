# syntax=docker/dockerfile:1
# Builds defra-people-api per Defra .NET container standards (github.com/DEFRA/defra-docker-dotnetcore).
ARG PARENT_VERSION=2.1.9-dotnet10.0

# Development stage: SDK-based parent image used to restore, test and publish the app.
FROM defradigital/dotnetcore-development:$PARENT_VERSION AS development
ENV TZ="Europe/London"
ARG PARENT_VERSION

# Label the development image with the parent image it extends.
LABEL uk.gov.defra.parent-image=defradigital/dotnetcore-development:${PARENT_VERSION}

# Create directories for app and test files to be copied into.
RUN mkdir -p ./defra-people-api/ ./defra-people-api-tests/

# Install test dependencies.
COPY --chown=dotnet:dotnet defra-people-api-tests/*.csproj ./defra-people-api-tests/
RUN dotnet restore ./defra-people-api-tests/defra-people-api-tests.csproj

# Install app dependencies.
COPY --chown=dotnet:dotnet defra-people-api/*.csproj ./defra-people-api/
RUN dotnet restore ./defra-people-api/defra-people-api.csproj

# Copy in test and app code.
COPY --chown=dotnet:dotnet defra-people-api-tests/ ./defra-people-api-tests/
COPY --chown=dotnet:dotnet defra-people-api/ ./defra-people-api/

# Build the app.
RUN dotnet publish ./defra-people-api/ -c Release -o /home/dotnet/out

# Expose the app on a defined port, configurable via a build argument. 8080 matches the
# /health/live and /health/ready probes (see Endpoints/HealthChecks/Healthcheck.cs).
ARG PORT=8080
ENV PORT=${PORT}
EXPOSE ${PORT}

# Exec form needs a script to interpolate ${PORT}; the script itself execs dotnet so it still
#   receives signals directly rather than a shell.
# Root-owned so the dotnet user can't modify its own startup script; switch back afterwards.
USER root
COPY --chown=root:root dev-entrypoint.sh ./
RUN chmod +x ./dev-entrypoint.sh
USER dotnet
CMD ["./dev-entrypoint.sh"]

# Production stage
FROM defradigital/dotnetcore:$PARENT_VERSION AS production
ARG PARENT_VERSION

# Label the production image with the parent image it extends.
LABEL uk.gov.defra.parent-image=defradigital/dotnetcore-development:${PARENT_VERSION} \
      org.opencontainers.image.title="defra-people-api" \
      org.opencontainers.image.description="Defra People API - Microsoft Graph user lookup service" \
      org.opencontainers.image.vendor="DEFRA"

# Expose the app on a defined port, configurable via a build argument.
ARG PORT=8080
ENV ASPNETCORE_URLS=http://*:${PORT} \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE ${PORT}

# Switch to root temporarily to lock down file permissions.
USER root

# Copy the compiled app from the development build stage, assigning root ownership to
#   prevent modification by other users.
COPY --from=development --chown=root:root /home/dotnet/out/ ./

# Remove write permissions, then drop back to the unprivileged user.
RUN chmod -R a-w /home/dotnet
USER dotnet

# Set the default command for production to run the compiled app.
CMD ["dotnet", "defra-people-api.dll"]

