# Architecture boundaries

## Target ownership map

| Concern | Authoritative owner | Allowed platform variation | Forbidden shortcut |
|---|---|---|---|
| Logical path syntax | Small pure contract in the lowest dependency layer that needs it | None; canonical `/` | Per-module string replacement |
| Physical root defaults | Infrastructure/composition root | Windows/Linux/macOS root adapters | One broad platform service |
| Filesystem equality/link/mode behavior | Infrastructure filesystem boundary | Root/volume-aware semantics and leaf native operations | `OperatingSystem.IsWindows()` as the universal comparer |
| Storage/control-plane migration | Infrastructure/storage/control-plane owners | Host binding and root adapters | Reinterpreting foreign absolute paths |
| Secret provider and migration | Security module plus bootstrap composition | DPAPI, Keychain, Secret Service, explicit headless provider | Auto fallback to unsupported/insecure file storage |
| ASP.NET Data Protection bootstrap | Infrastructure composition before secret consumers | OS/profile key-ring protection | Protecting the key ring with a secret that depends on the same ring |
| Generic workspace process execution | Existing AgentFramework Core workspace runtime | OS-specific executable/env/kill leaf behavior if proven necessary | A second local process runner |
| Workbench runtime-node planning | Workbench | Optional terminal/elevation presentation adapters | PowerShell text as the ordinary execution source |
| Manager supervision/recovery | Manager | WMI, Linux proc, macOS discovery adapters | Name-only termination |
| MCP local stdio | MCP integration | Runtime/package executable adapters | Separate process/environment stack |
| Plugin host tools | Plugin integration consuming shared execution ports | Dependency-specific probes | Direct construction of process hosts |
| Process-domain semantics/recovery | `Processes` and its drivers | Process strategies may require declared host capabilities | Process semantics in MAF/Infrastructure/platform service |
| UI capability presentation | Owning UI/module consuming capability descriptors | Available/unavailable/remediation states | Inferring support only from OS name |

## Narrow-adapter rule

An OS-specific abstraction is justified only when all of the following are true:

1. behavior differs materially across target operating systems or profiles;
2. portable .NET APIs plus configuration cannot express the behavior safely;
3. at least two callers need a stable contract, or the native implementation must be isolated for dependency/runtime safety;
4. the contract has one purpose and one owner;
5. tests can replace the implementation without mocking the entire OS.

Examples that meet this threshold:

- secure-vault provider;
- application data root defaults;
- native permission hardener;
- Manager process discovery;
- optional terminal presentation;
- Data Protection key-ring protector.

Examples that normally do not:

- ordinary `Path.Combine`;
- direct `ProcessStartInfo.ArgumentList`;
- `FileStream`;
- URL construction;
- process-domain decisions;
- a boolean collection of every platform feature.

## Path model

```text
Logical path
  - application identifier / storage locator / artifact route
  - serialized with "/"
  - ordinal semantics
  - no root, drive, UNC, dot traversal, or host separators

Physical host path
  - native filesystem address
  - never serialized into a portable locator
  - may be persisted only as versioned host-bound configuration
  - compared through root/volume filesystem semantics

URI / route
  - URI semantics; "/" is not a filesystem separator

Executable identity
  - explicit path or capability-owned command name
  - resolved by host executable policy

Script / opaque command text
  - language-specific content
  - never normalized as a path
```

Legacy backslash compatibility belongs only at known logical-path deserialization boundaries. On Unix, backslash is otherwise a legal filename character.

## Process model

```text
Runtime intent / node metadata
    -> pure typed plan compiler
    -> capability + authority validation
    -> executable resolver / environment policy
    -> authoritative process host + lifecycle registry
    -> bounded/redacted receipt
```

Terminal windows and elevation are presentation/privilege capabilities around the plan, not the source of the plan.

## Process/MAF boundary

`Processes` owns:

- process strategy and driver selection;
- process-domain eligibility and missing-capability decisions;
- recovery/escalation semantics;
- process receipts/evidence policy;
- template validation and alternate strategy.

MAF/AgentFramework owns:

- generic agent execution;
- generic workspace/process/tool ports;
- SDK adaptation;
- generic capability invocation mechanics.

Host adapters expose facts and execution ports. They do not grant workspace authority, approvals, or process semantics.

## Secret bootstrap graph

A valid bootstrap graph is acyclic:

```text
OS / explicit headless credential
    -> key-ring protector or secure vault bootstrap
    -> ASP.NET Data Protection key ring and/or vault wrapping key
    -> legacy secret/control-plane decryption
    -> migrated vault records
    -> runtime secret resolution
```

A secret protected by the Data Protection ring cannot be the only material required to decrypt that same ring.

## Headless-first support

The primary cross-platform support claim is a headless Web host. Optional desktop, terminal, Docker, native process discovery, interactive keyring, and FileTools capabilities are tested and reported independently. Their absence must not make the core host lie or fail unless the selected profile explicitly requires them.
