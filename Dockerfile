# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer-cached NuGet restore
COPY FauxHR.sln ./
COPY FauxHR.App.Server/FauxHR.App.Server.csproj FauxHR.App.Server/
COPY FauxHR.App/FauxHR.App.csproj FauxHR.App/
COPY FauxHR.Core/FauxHR.Core.csproj FauxHR.Core/
COPY FauxHR.Modules.ExitStrategy/FauxHR.Modules.ExitStrategy.csproj FauxHR.Modules.ExitStrategy/
COPY FauxHR.Modules.CrmiAuthoring/FauxHR.Modules.CrmiAuthoring.csproj FauxHR.Modules.CrmiAuthoring/

RUN dotnet restore

# Copy everything else and publish
COPY . .
RUN dotnet publish FauxHR.App.Server/FauxHR.App.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup -d /app -s /sbin/nologin appuser

COPY --from=build /app/publish .

USER appuser

# HTTP only — Cloudflare Tunnel handles TLS
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "FauxHR.App.Server.dll"]
