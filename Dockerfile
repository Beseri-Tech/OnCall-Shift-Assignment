# syntax=docker/dockerfile:1

# ---- web: build the React app ----
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# ---- api: publish the ASP.NET Core app ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY api/Rota.Core/Rota.Core.csproj Rota.Core/
COPY api/Rota.Api/Rota.Api.csproj Rota.Api/
RUN dotnet restore Rota.Api/Rota.Api.csproj
COPY api/Rota.Core/ Rota.Core/
COPY api/Rota.Api/ Rota.Api/
RUN dotnet publish Rota.Api/Rota.Api.csproj -c Release -o /app --no-restore

# ---- runtime: API serves the built web app from wwwroot ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Npgsql/.NET probe for GSSAPI at startup; without it the log shows a harmless but noisy error.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Rota.Api.dll"]
