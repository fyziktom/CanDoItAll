# Core current state and delta

## Positive foundations

- The solution targets neutral `net10.0`; no Windows-only TFM, WinForms/WPF, or `System.Drawing.Common` dependency was found in the inspected source.
- Storage and workspace code already has root containment and reparse checks that can be consolidated rather than discarded.
- Control-plane JSON writers already use temporary files in some paths.
- Security now has explicit abstractions and a vault-provider enum.
- The base app configuration keeps desktop launch disabled.

## Critical current defects

- Shared development configuration is Windows-only.
- Four path owners apply incompatible separator/case/containment semantics.
- Persistent physical paths are not host-bound.
- Storage and key/vault writes do not share a process-safe atomic contract.
- Unix permission hardening is absent from the inspected secret/control-plane paths.
- Auto secret selection advertises macOS/Linux providers that are unsupported.
- The file vault stores its key beside ciphertext.
- Data Protection key-ring at-rest protection is unspecified.
- CI is disabled and lacks a macOS application gate.

## Scope introduced by recent refactors

The latest MAF refactor added `Security.Abstractions` and clearer run/process ownership. The core bundle must avoid placing platform-specific process semantics in a new foundation project. It may create a small path/filesystem abstraction only if dependency analysis proves existing owners cannot share a pure contract without reverse references.
