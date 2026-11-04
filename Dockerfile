# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada separada (só muda quando um .csproj/props muda) para acelerar rebuilds
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Estoka.Domain/Estoka.Domain.csproj src/Estoka.Domain/
COPY src/Estoka.Application/Estoka.Application.csproj src/Estoka.Application/
COPY src/Estoka.Infrastructure/Estoka.Infrastructure.csproj src/Estoka.Infrastructure/
COPY src/Estoka.Web/Estoka.Web.csproj src/Estoka.Web/
RUN dotnet restore src/Estoka.Web/Estoka.Web.csproj

COPY src/ src/
# Sem --no-restore: o SDK só inclui os assets do Blazor (blazor.web.js) quando enxerga os .razor no restore
RUN dotnet publish src/Estoka.Web/Estoka.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_GCHeapHardLimitPercent=0x4B \
    TZ=America/Sao_Paulo

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Usuário sem privilégios já existente nas imagens oficiais do .NET
USER $APP_UID
EXPOSE 8080

# O app escuta em $PORT quando definido (Render); caso contrário, 8080.
ENTRYPOINT ["dotnet", "Estoka.Web.dll"]
