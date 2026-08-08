# Manager supervision and process discovery

## Primary ownership

A process launched by Manager/runtime is registered immediately with:

- PID;
- process start identity/time;
- executable identity;
- normalized argv hash/signature;
- authorized workspace/root;
- current user/owner;
- parent/lease/supervisor ID;
- lifecycle state;
- non-secret capability/recipe ID.

This registry is the primary truth.

## Recovery discovery

OS adapters may recover evidence after Manager restart:

### Windows

WMI/System.Management behind a Windows-only Manager adapter.

### Linux

Bounded `/proc` reads or equivalent. Handle:

- process exit during read;
- permission denied;
- kernel threads/empty cmdline;
- symlink/exe resolution;
- PID reuse;
- namespaces/containers;
- missing parent.

### macOS

Use a proven native API or a strictly controlled adapter. If command output is used, pin non-localized format and test parsing/races/permissions. Do not authorize action from locale-dependent human output.

## Termination authorization

Never terminate with only:

- process name;
- substring;
- executable basename;
- workspace text in command line.

Require PID/start identity plus sufficient registry/owner/executable/command/workspace evidence. Ambiguity yields a manual cleanup diagnostic.

## Supervisors and watchers

Share process lifecycle and Core watcher convergence primitives, but retain separate domain owners for:

- dotnet watch;
- Tailwind;
- tuning;
- other supervisors.

Events schedule work; deterministic fingerprint/rescan confirms state.
