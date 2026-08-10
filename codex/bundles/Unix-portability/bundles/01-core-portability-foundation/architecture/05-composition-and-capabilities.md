# Composition and capabilities

## Registration pattern

The composition root selects implementations from an explicit host profile.

```text
Host profile
  -> root defaults
  -> filesystem semantics/permission adapter
  -> key-ring protector
  -> secret provider
  -> storage/control-plane services
  -> optional capability probes
  -> readiness/support profile
```

Avoid hidden lazy fallback during first use.

## Mandatory versus optional

Mandatory for production headless startup:

- usable control-plane and workspace roots;
- secure key/vault bootstrap;
- configured database and migrations;
- required storage provider;
- no ambiguous pending migration.

Optional unless the selected profile requires them:

- desktop open/reveal;
- interactive terminal;
- native process recovery discovery;
- Docker;
- FileTools desktop package;
- interactive Keychain/Secret Service;
- runtime nodes/MCP/plugins.

## Capability descriptor

Recommended information:

- stable capability ID;
- state: available, unavailable, unsupported, misconfigured, unverified;
- support profile;
- reason code;
- remediation;
- implementation/dependency version;
- security/execution boundary;
- last probe;
- sensitive details omitted.

UI and process strategies consume this descriptor. They do not infer support from `OperatingSystem.Is*`.

## OS checks

Allowed:

- composition selecting a leaf adapter;
- root defaults;
- native permission/process/keyring implementation;
- executable candidate behavior;
- actual-host characterization test.

Not allowed without a documented exception:

- domain/process semantic branch;
- path authorization branch spread across modules;
- UI business decision based only on OS;
- an OS branch to weaken security or skip validation.

## A05 implementation decision

### Responsibility and owner

The isolated responsibility is host-profile resolution and projection of already-owned
runtime facts into a non-authorizing readiness snapshot. The current facts remain owned
by their existing leaf implementations: Infrastructure owns paths and filesystem policy,
Security owns the selected vault and its probe, FileTools owns desktop launch capability,
and Processes retains process-domain semantics.

`CanDoItAll.Composition` will own:

- one resolved, typed runtime host profile;
- a capability snapshot projector and provider;
- a mandatory-capability startup validator and health check;
- the Web API projection of the same snapshot.

No new project or project reference is required. Composition already references every
leaf implementation it wires, and no lower-level project will reference Composition or
Web.

### Selected pattern

Use a small resolver plus read-only descriptor catalog. This is the narrow factory/catalog
shape already approved by the bundle pattern record: the resolver converts configuration
and host facts into one explicit profile, while the catalog maps concrete probe results to
stable descriptors. It does not execute tools, grant access, choose process policy, or
locate services dynamically.

Adding capability fields to `Infrastructure.Readiness` was rejected because it would make
Infrastructure know Security and FileTools implementations. A broad `IPlatformService`
was rejected because it would aggregate unrelated authority and become a service locator.
Direct OS booleans in UI or process code were rejected because they cannot express
misconfigured, unavailable, or actual-host-unverified states.

### Testability and gate proof

- Profile resolution and capability projection are pure and testable with explicit host
  facts; tests cover Windows interactive/headless, Linux interactive/headless, macOS
  interactive/headless, and the development test profile.
- A negative test proves an unavailable mandatory vault blocks readiness.
- A semantic positive proves unavailable optional adapters do not block headless core
  readiness.
- A redaction regression proves provider remediation containing a path or secret-like
  value is not copied into the public descriptor.
- Composition tests prove one registration for every mandatory contract and at most one
  optional desktop adapter.
- Architecture tests reject a broad `IPlatformService`, reverse MAF/Processes references,
  and OS branching in process-domain semantics.
- The existing Web startup smoke proves the composition root still wires the application.

No partial class is added, and the capability provider can be constructed independently
of `Program` or a MAF runtime.
