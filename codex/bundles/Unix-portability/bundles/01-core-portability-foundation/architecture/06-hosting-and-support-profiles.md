# Hosting and support profiles

## Headless core

The Web host must run without:

- desktop session;
- terminal application;
- FileTools desktop capability;
- interactive keyring when a headless secret provider is configured;
- Manager;
- MCP/local tool execution.

## Root conventions

Exact final paths are decided in A03, but the policy distinguishes:

- application data/content;
- configuration/control plane;
- protected key/vault state;
- logs/diagnostics;
- transient runtime data;
- user workspace.

Windows, XDG/service Linux, and macOS Application Support/state/log conventions are explicit. Service profiles use dedicated owned directories rather than the repository checkout.

## Publishing

Initial proof uses framework-dependent publish. Do not combine portability with trimming, single-file, native AOT, or self-contained changes.

Required artifact claims:

- win-x64;
- linux-x64;
- osx-arm64;
- osx-x64 publish.

An RID publish is not a support claim until actual runtime evidence exists.

## Services

Linux systemd and macOS launchd runbooks include:

- service identity;
- working directory;
- environment/config source;
- root ownership/modes;
- PostgreSQL readiness;
- restart/stop timeout;
- logging;
- backup/upgrade/rollback;
- support/capability diagnostics.

## Observability

Readiness may expose:

- platform/profile;
- database state;
- migration state;
- secret provider state;
- control-plane/storage accessibility;
- optional capabilities.

It must not expose secret identifiers/values, complete environment, or unnecessary full paths.

## A06 implementation decision

### Ownership and dependency direction

`CanDoItAll.Composition` owns a versioned, read-only deployment-support manifest because it already owns host-profile composition and support claims. The Web host publishes that same manifest as `runtime-support.json` and exposes a typed operational projection that combines it with the existing runtime-readiness and host-capability snapshots. The projection reports states and reason codes only; it never adds a second path, database, or secret probe.

Unix install assets own only artifact activation. They copy a framework-dependent publish into an immutable release directory and atomically switch a validated release-id state file. They do not select providers, invent roots, rewrite persisted paths, manage PostgreSQL data, elevate privileges, or bypass startup validators. systemd and launchd remain operator-owned integration layers.

The Windows installer remains the owner of Windows shortcut and installed-database integration. Local development uses conditional project references to the sibling Components and FileTools repositories when both source trees exist; builds without those sibling trees retain the existing NuGet references.

### Selected shape

- one strongly typed, embedded deployment-support manifest with schema validation;
- one redacted operational snapshot projector over existing owner-produced facts;
- one stable `/api/runtime/operations` endpoint plus the existing `/health` and capability endpoint;
- small POSIX release install/run/rollback scripts with no automatic elevation;
- declarative systemd and launchd templates plus one cross-platform operator runbook.

Rejected alternatives are a broad deployment/platform service, copying physical roots into diagnostics, duplicating vault/database readiness, embedding service management in the application, and treating a successful cross-RID publish as actual-host support.

### Testability and gate proof

- codec tests reject malformed schemas, duplicate RIDs/profiles, unknown enums, missing prerequisites, and misleading verified macOS claims;
- projection tests prove operational JSON contains support/readiness/capability state but no full configured roots, connection strings, or secret values;
- the publish gate verifies all four framework-dependent RID artifacts contain the identical support manifest;
- Linux tests install two synthetic releases, start from the active release, switch, rollback, and preserve the prior release;
- Windows and Linux smoke tests start, report health/operations, shut down, and restart from clean publish directories outside the repository;
- static checks reject automatic elevation, provider/path policy duplication, and unbounded diagnostic fields.
