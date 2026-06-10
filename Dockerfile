# ── Stage 1: Restore & Build ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only csproj files first to leverage Docker layer cache for restore
COPY src/FIAP.CatalogAPI.Domain/FIAP.CatalogAPI.Domain.csproj           src/FIAP.CatalogAPI.Domain/
COPY src/FIAP.CatalogAPI.Application/FIAP.CatalogAPI.Application.csproj  src/FIAP.CatalogAPI.Application/
COPY src/FIAP.CatalogAPI.Infrastructure/FIAP.CatalogAPI.Infrastructure.csproj src/FIAP.CatalogAPI.Infrastructure/
COPY src/FIAP.CatalogAPI.Api/FIAP.CatalogAPI.Api.csproj                  src/FIAP.CatalogAPI.Api/

RUN dotnet restore src/FIAP.CatalogAPI.Api/FIAP.CatalogAPI.Api.csproj

# Copy full source and publish
COPY . .
RUN dotnet publish src/FIAP.CatalogAPI.Api/FIAP.CatalogAPI.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "FIAP.CatalogAPI.Api.dll"]
