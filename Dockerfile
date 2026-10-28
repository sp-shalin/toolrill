# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore src/ToolRill/ToolRill.csproj

RUN dotnet publish src/ToolRill/ToolRill.csproj \
    -c Release \
    -o /app/publish \
    -p:UseAppHost=false \
    --no-restore

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS sdk

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.nuget/packages \
    && chown -R "$APP_UID" /home/app

ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_CLI_HOME=/home/app \
    NUGET_PACKAGES=/home/app/.nuget/packages

WORKDIR /projects

COPY --chown=app:app Directory.Build.props ./
COPY --chown=app:app samples/StdioServer/ ./samples/StdioServer/

USER $APP_UID

RUN dotnet restore samples/StdioServer/StdioServer.csproj \
    && dotnet build samples/StdioServer/StdioServer.csproj -c Debug --no-restore

WORKDIR /app

COPY --from=build /app/publish .
COPY deploy/compose/toolrill.json /config/toolrill.json

EXPOSE 3001
STOPSIGNAL SIGTERM

HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 2 http://127.0.0.1:3001/health || exit 1

ENTRYPOINT ["dotnet", "ToolRill.dll"]
CMD ["start", "--host", "default", "--config", "/config/toolrill.json"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_ENVIRONMENT=Production

WORKDIR /app

COPY --from=build /app/publish .
COPY deploy/docker/toolrill.json /config/toolrill.json

USER $APP_UID

EXPOSE 3001
STOPSIGNAL SIGTERM

HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 2 http://127.0.0.1:3001/health || exit 1

ENTRYPOINT ["dotnet", "ToolRill.dll"]
CMD ["start", "--host", "default", "--config", "/config/toolrill.json"]