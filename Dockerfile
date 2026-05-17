FROM node:24-alpine AS web-build
WORKDIR /src

COPY src/Finance.Web/package*.json ./src/Finance.Web/
RUN cd src/Finance.Web && npm ci

COPY src/Finance.Web ./src/Finance.Web
COPY src/Finance.Api ./src/Finance.Api
RUN cd src/Finance.Web && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src

COPY Finance.slnx ./
COPY src/Finance.Core/Finance.Core.csproj ./src/Finance.Core/
COPY src/Finance.Data/Finance.Data.csproj ./src/Finance.Data/
COPY src/Finance.Api/Finance.Api.csproj ./src/Finance.Api/
RUN dotnet restore src/Finance.Api/Finance.Api.csproj

COPY src ./src
COPY --from=web-build /src/src/Finance.Api/wwwroot ./src/Finance.Api/wwwroot
RUN dotnet publish src/Finance.Api/Finance.Api.csproj -c Release -o /app/publish --no-restore /p:SkipReactBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=api-build /app/publish ./

ENTRYPOINT ["sh", "-c", "dotnet Finance.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
