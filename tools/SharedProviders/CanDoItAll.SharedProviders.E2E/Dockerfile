ARG DOTNET_RUNTIME_VERSION=10.0.10
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION}

ARG BUILD_DATE
ARG BUILD_REVISION
ARG BUILD_SOURCE_FINGERPRINT
ARG BUILD_VERSION
LABEL org.opencontainers.image.title="CanDoItAll shared-provider evidence tools" \
      org.opencontainers.image.description="Published evidence tools for the isolated shared-provider fixture" \
      org.opencontainers.image.source="https://github.com/fyziktom/CanDoItAll" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.vendor="CanDoItAll" \
      org.opencontainers.image.documentation="https://github.com/fyziktom/CanDoItAll/blob/main/docs/testing.md" \
      org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.revision="${BUILD_REVISION}" \
      org.opencontainers.image.version="${BUILD_VERSION}" \
      io.candoitall.source-fingerprint="${BUILD_SOURCE_FINGERPRINT}"
WORKDIR /runner
COPY . .
USER 1654:1654
ENTRYPOINT ["dotnet", "/runner/CanDoItAll.SharedProviders.E2E.dll"]
