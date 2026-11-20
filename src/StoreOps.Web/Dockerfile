# Build context: repository root
#   docker build -f src/StoreOps.Web/Dockerfile -t storeops-web .

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/StoreOps.Core/StoreOps.Core.csproj src/StoreOps.Core/
COPY src/StoreOps.Web/StoreOps.Web.csproj src/StoreOps.Web/
RUN dotnet restore src/StoreOps.Web/StoreOps.Web.csproj
COPY src/StoreOps.Core/ src/StoreOps.Core/
COPY src/StoreOps.Web/ src/StoreOps.Web/
RUN dotnet publish src/StoreOps.Web/StoreOps.Web.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG APP_VERSION=local
ENV APP_VERSION=$APP_VERSION \
    ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
# Non-root user provided by the .NET base images
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "StoreOps.Web.dll"]
