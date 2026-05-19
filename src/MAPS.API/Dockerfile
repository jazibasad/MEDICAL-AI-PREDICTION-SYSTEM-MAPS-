# ============================================================
# MAPS.API — Multi-Stage Dockerfile
# Stage 1: Build  → restores, builds, publishes
# Stage 2: Runtime → minimal ASP.NET runtime image
# ============================================================

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (layer cache optimization)
COPY MAPS.sln .
COPY Directory.Build.props .
COPY src/MAPS.Shared/MAPS.Shared.csproj         src/MAPS.Shared/
COPY src/MAPS.ML/MAPS.ML.csproj                 src/MAPS.ML/
COPY src/MAPS.API/MAPS.API.csproj               src/MAPS.API/

# Restore NuGet packages (cached unless .csproj files change)
RUN dotnet restore src/MAPS.API/MAPS.API.csproj

# Copy full source
COPY src/MAPS.Shared/ src/MAPS.Shared/
COPY src/MAPS.ML/     src/MAPS.ML/
COPY src/MAPS.API/    src/MAPS.API/

# Build and publish
RUN dotnet publish src/MAPS.API/MAPS.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r mapsapi && useradd -r -g mapsapi mapsapi
RUN chown -R mapsapi:mapsapi /app

COPY --from=build /app/publish .

# Create directories for models (will be mounted as volumes)
RUN mkdir -p /app/models/onnx /app/models/mlnet

USER mapsapi

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=5 \
    CMD curl -f http://localhost:5000/api/health || exit 1

ENTRYPOINT ["dotnet", "MAPS.API.dll"]
