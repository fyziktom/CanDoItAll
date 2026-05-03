# Architecture Review Gate 1

## Decision

- `Proceed` to subbundle 09 process-flow integration.

## Findings

- No blocking package or compile/test failures were found in the subbundle 01-07 foundation.
- No preview A2A SDK types were found in `CanDoItAll.AgentFramework.Models`, `CanDoItAll.AgentFramework.Core`, or `CanDoItAll.Modules.Processes`.
- Preview A2A package use is isolated to `CanDoItAll.AgentFramework.Maf` for remote A2A tool construction and `CanDoItAll.AgentFramework.Hosting` for A2A host card/server registration.
- MAF handoff workflow construction remains in `CanDoItAll.AgentFramework.Maf`; Models/Core expose CanDoItAll-owned contracts and execution options.
- Tool availability now uses typed workspace profiles, and runtime attachment denies disallowed workspace tool families for read-only agents.
- Governed process context policy now skips compaction with explicit progress logs, and approval continuations reject missing or incompatible serialized MAF session state instead of silently replaying approval responses in a fresh session.

## Accepted Risks

- `Microsoft.Agents.AI.A2A` and `Microsoft.Agents.AI.Hosting.A2A` are still preview packages in the 1.3 package line. This is acceptable because preview types are kept behind Maf/Hosting boundaries.
- Runtime compaction knobs remain in Maf's internal agent-runtime JSON configuration rather than a public Models metadata helper. This does not block process integration because governed process behavior is enforced by `WorkspaceExecutionAuditContext` and covered by integration tests, but it should be revisited if an editor/API surface needs to configure those knobs.
- Existing NU1902 and NU1904 advisory warnings are unrelated to the MAF cooperation foundation and remain outside this bundle gate.

## Proof Reviewed

- MAF package/model/tool/context targeted tests recorded in `reviews/01-execution-report.md`.
- `dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore -m:1`: passed with existing warnings.
- `git diff --check`: passed with existing LF-to-CRLF warnings only.
- Source grep confirmed A2A preview use is isolated to Maf/Hosting and no A2A package reference exists in Models/Core/Processes.

## Process Integration Entry Gate

Subbundle 09 may start. It must keep process semantics in `CanDoItAll.Modules.Processes`, consume typed runtime options/tool profiles instead of preview SDK types, preserve finalizer validation, and prove artifact handoff behavior through dispatch/runtime tests.
