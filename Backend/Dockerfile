FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS build
WORKDIR /src
COPY src/Splice.Backend.Api/Splice.Backend.Api.csproj src/Splice.Backend.Api/
RUN dotnet restore src/Splice.Backend.Api/Splice.Backend.Api.csproj
COPY src/Splice.Backend.Api/ src/Splice.Backend.Api/
RUN dotnet publish src/Splice.Backend.Api/Splice.Backend.Api.csproj \
    --configuration Release --no-restore --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.7-noble@sha256:55e37c7795bfaf6b9cc5d77c155811d9569f529d86e20647704bc1d7dd9741d4 AS runtime
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
WORKDIR /app
RUN mkdir -p /app/data/replays && chown -R "$APP_UID:$APP_UID" /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Splice.Backend.Api.dll"]
