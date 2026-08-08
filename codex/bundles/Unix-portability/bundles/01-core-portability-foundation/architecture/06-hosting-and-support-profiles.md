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
