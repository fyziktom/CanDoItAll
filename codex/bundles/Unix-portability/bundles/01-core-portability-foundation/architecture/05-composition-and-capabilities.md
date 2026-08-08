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
