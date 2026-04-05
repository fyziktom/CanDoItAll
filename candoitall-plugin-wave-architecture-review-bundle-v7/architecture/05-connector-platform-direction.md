# Connector platform direction

## Problem

Closed enums like `ProviderKind` and `ResourceKind` are not an extensibility strategy for the upcoming wave.

## Proposed platform

Introduce connector descriptors / manifests:

- `ConnectorKey` (string)
- `Category`
- `DisplayName`
- `ConfigSchema`
- `SecretRequirements`
- `Capabilities`
- `HealthCheckStrategy`
- `CommandSurface`
- `NodeHookCapabilities`
- `PolicyExposure`
- `Pull/Push/Sync semantics`

## First-party connectors that should migrate onto the same seam

- current OpenAI provider
- current Ollama providers
- future email connectors
- future LinkedIn connector
- future custom REST/API connectors

## Transition rule

First-party built-ins may remain bundled in the repo, but the extension seam must become descriptor-driven now.
