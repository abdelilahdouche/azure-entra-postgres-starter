# Build context: repository root
#   docker build -f src/StoreOps.Jobs/Dockerfile -t storeops-jobs .
# The command (migrate | cleanup) is passed as container args by each Container Apps Job.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/StoreOps.Core/StoreOps.Core.csproj src/StoreOps.Core/
COPY src/StoreOps.Jobs/StoreOps.Jobs.csproj src/StoreOps.Jobs/
RUN dotnet restore src/StoreOps.Jobs/StoreOps.Jobs.csproj
COPY src/StoreOps.Core/ src/StoreOps.Core/
COPY src/StoreOps.Jobs/ src/StoreOps.Jobs/
RUN dotnet publish src/StoreOps.Jobs/StoreOps.Jobs.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:11.0 AS runtime
ARG APP_VERSION=local
ENV APP_VERSION=$APP_VERSION
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "StoreOps.Jobs.dll"]
