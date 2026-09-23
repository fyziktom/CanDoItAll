# syntax=docker/dockerfile:1

ARG DOTNET_SDK_VERSION=10.0.302
ARG DOTNET_RUNTIME_VERSION=10.0.10
ARG BUSYBOX_VERSION=1.37.0-musl

FROM busybox:${BUSYBOX_VERSION} AS healthcheck

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /source

COPY CanDoItAll.SharedProviders.TestUpstream.csproj ./
RUN --mount=type=cache,id=shared-providers-test-upstream-nuget,target=/root/.nuget/packages \
    dotnet restore CanDoItAll.SharedProviders.TestUpstream.csproj

COPY . ./
RUN --mount=type=cache,id=shared-providers-test-upstream-nuget,target=/root/.nuget/packages \
    dotnet publish CanDoItAll.SharedProviders.TestUpstream.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS runtime
ARG BUILD_DATE
ARG BUILD_REVISION
ARG BUILD_SOURCE_FINGERPRINT
ARG BUILD_VERSION

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.description="Deterministic shared-provider E2E upstream" \
      org.opencontainers.image.documentation="https://github.com/fyziktom/CanDoItAll/tree/main/tests/Support/CanDoItAll.SharedProviders.TestUpstream" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.revision="${BUILD_REVISION}" \
      org.opencontainers.image.source="https://github.com/fyziktom/CanDoItAll" \
      org.opencontainers.image.title="CanDoItAll.SharedProviders.TestUpstream" \
      org.opencontainers.image.vendor="CanDoItAll" \
      org.opencontainers.image.version="${BUILD_VERSION}" \
      io.candoitall.source-fingerprint="${BUILD_SOURCE_FINGERPRINT}"

WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=healthcheck /bin/busybox /usr/local/bin/busybox
COPY --from=build --chown=app:app /app/publish ./
USER $APP_UID

ENTRYPOINT ["dotnet", "CanDoItAll.SharedProviders.TestUpstream.dll"]
