# P7-007 - Provider/resource/connector architecture is still a closed enum-and-switch seam

- Severity: Critical
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-005

## Problem

Workspace and Resources still rely on ProviderKind and ResourceKind enums, closed adapter registration, and per-kind switch logic. That is not a viable base for email, LinkedIn, and custom API connectors with descriptors, config schemas, secrets, health checks, capabilities, and node hooks.

## Required direction

Introduce a manifest/descriptor-driven connector platform. Profiles should bind to connector keys and schema descriptors, not to closed enums. Resources and providers should become first-party plugin descriptors using the same extension seam.

## Closure proof

ProviderKind/ResourceKind are no longer the extensibility seam for new connectors; connector descriptors/manifests exist; new first-party connectors register through the descriptor platform.
