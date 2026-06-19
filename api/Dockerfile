# syntax=docker/dockerfile:1
# .NET 10 gateway. Build context must be the repo's api/ directory.
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the project files first so layer caching survives source-only changes.
COPY Api/Api.csproj Api/
COPY Application/Application.csproj Application/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore Api/Api.csproj

COPY . .
RUN dotnet publish Api/Api.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF renders via SkiaSharp (needs native font/graphics libs); Npgsql probes for
# Kerberos/GSSAPI on connect (libgssapi-krb5-2) — install both so neither logs errors.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libfontconfig1 libfreetype6 fonts-dejavu-core libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production
# Render injects $PORT; bind to it (fall back to 8080 for local `docker run`).
ENTRYPOINT ["sh", "-c", "dotnet Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
