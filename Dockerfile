# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Project files first: restore is cached and only re-runs when a dependency actually
# changes, not on every source edit. Only the four projects the API transitively needs are
# copied — pulling in the test projects would invalidate this layer for a change that
# cannot affect the image.
COPY src/Flow.Domain/Flow.Domain.csproj src/Flow.Domain/
COPY src/Flow.Application/Flow.Application.csproj src/Flow.Application/
COPY src/Flow.Infrastructure/Flow.Infrastructure.csproj src/Flow.Infrastructure/
COPY src/Flow.API/Flow.API.csproj src/Flow.API/

RUN dotnet restore src/Flow.API/Flow.API.csproj

COPY src/ src/

RUN dotnet publish src/Flow.API/Flow.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Runs as an unprivileged user. A container that does not need root should not have it.
RUN addgroup -S flow && adduser -S flow -G flow

# curl is used by the container healthcheck below.
RUN apk add --no-cache curl icu-libs

# Needed for pt-BR currency and date formatting in log and error output.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

COPY --from=build --chown=flow:flow /app/publish ./

USER flow
EXPOSE 8080

# Readiness, not liveness: the orchestrator should only route traffic here once
# MongoDB is actually reachable.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "Flow.API.dll"]
