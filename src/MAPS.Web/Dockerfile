# ============================================================
# MAPS.Web — Multi-Stage Dockerfile
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MAPS.sln .
COPY Directory.Build.props .
COPY src/MAPS.Shared/MAPS.Shared.csproj src/MAPS.Shared/
COPY src/MAPS.Web/MAPS.Web.csproj       src/MAPS.Web/

RUN dotnet restore src/MAPS.Web/MAPS.Web.csproj

COPY src/MAPS.Shared/ src/MAPS.Shared/
COPY src/MAPS.Web/    src/MAPS.Web/

RUN dotnet publish src/MAPS.Web/MAPS.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
RUN groupadd -r mapsweb && useradd -r -g mapsweb mapsweb
RUN chown -R mapsweb:mapsweb /app

COPY --from=build /app/publish .

USER mapsweb

EXPOSE 8080

ENTRYPOINT ["dotnet", "MAPS.Web.dll"]
