# syntax=docker/dockerfile:1

ARG DOTNET_SDK_VERSION=10.0.302
ARG DOTNET_RUNTIME_VERSION=10.0.10
ARG BUSYBOX_VERSION=1.37.0-musl

FROM busybox:${BUSYBOX_VERSION} AS healthcheck

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /source

COPY . .
RUN --mount=type=cache,id=candoitall-nuget,target=/root/.nuget/packages \
    dotnet restore "src/App/CanDoItAll.Web/CanDoItAll.Web.csproj" \
    /p:UseLocalCanDoItAllLibraries=false

RUN --mount=type=cache,id=candoitall-nuget,target=/root/.nuget/packages \
    dotnet publish "src/App/CanDoItAll.Web/CanDoItAll.Web.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:UseLocalCanDoItAllLibraries=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS runtime
ARG BUILD_DATE
ARG BUILD_REVISION
ARG BUILD_VERSION=dev

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.description="CanDoItAll Blazor web application" \
      org.opencontainers.image.documentation="https://github.com/fyziktom/CanDoItAll/blob/main/docs/operations/containers.md" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.revision="${BUILD_REVISION}" \
      org.opencontainers.image.source="https://github.com/fyziktom/CanDoItAll" \
      org.opencontainers.image.title="CanDoItAll.Web" \
      org.opencontainers.image.vendor="CanDoItAll" \
      org.opencontainers.image.version="${BUILD_VERSION}"

WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=healthcheck /bin/busybox /usr/local/bin/busybox
COPY --from=build --chown=app:app /app/publish .

RUN mkdir -p \
        /data/config \
        /data/control-plane \
        /data/logs \
        /data/state \
        /data/workspace \
    && chown -R app:app /data

USER $APP_UID

ENTRYPOINT ["dotnet", "CanDoItAll.Web.dll"]
