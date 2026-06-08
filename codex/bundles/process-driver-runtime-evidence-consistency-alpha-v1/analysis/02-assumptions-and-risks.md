# Assumptions And Risks

## Assumptions
- Branch: `maf-processes-refactor`.
- The latest completed source state is the source of truth, not any previous bundle name.
- `.NET/Rust` transcript verifier and read-only process adapter are production code but are still explicitly verification-only.
- Core remains deterministic and dependency-clean.
- Driver abstractions remain contract-only.
- Runtime host/registry/selector/DI/manager-command work remains explicitly denied.

## Critical Path Risks
- Codex crash left report files complete but production code partially moved.
- Decomposition of verifier/adapter accidentally changes behavior or redaction/hash/audit semantics.
- Runtime evidence verifier accidentally starts owning retry/finalizer/projection behavior instead of describing contradictions.
- Process-module runtime-evidence adapter accidentally reads arbitrary files or attaches diagnostics as process mutations.
- Core starts referencing driver abstractions or driver packages.
- Driver package starts referencing Modules, Infrastructure, AgentFramework, EF, UI, workspace, or storage.
- Test proof becomes fixture-only or table-only instead of exercising production emitters and consumers.
- Future host roadmap wording implies registry/DI/manager command approval too early.

## Validation Risks
- Build-only proof is insufficient.
- Unit-only proof is insufficient for process adapter behavior.
- Source scans that only scan one file may miss csproj references, Core reverse references, or DI extension drift.
- Hash/redaction tests can pass with synthetic data while leaking actual sensitive fields in diagnostics.
- Runtime evidence consistency can be faked by checking non-empty descriptor collections without semantic contradiction cases.

## Reopen Triggers
- Any production code contains `IServiceCollection`, `AddScoped`, `AddSingleton`, `IProcessDriverRegistry`, `ProcessDriverRuntimeSelector`, `ProcessDriverHost`, `ManagerCommand`, `Process.Start`, `File.`, `Directory.`, `HttpClient`, `Graph`, `TransitionStep`, `ApplyFinalizer`, `ScheduleRetry`, `ClaimDispatch`, `DbContext`, `IStorage`, or workspace write behavior in the new driver packages or adapters.
- Any Core project references driver abstractions or driver packages.
- Any driver package references Modules, Infrastructure, AgentFramework, EF, UI, storage, workspace, or external connector packages.
- Any test accepts `VerificationOnly` or `ManagerReadonly` response with `NoMutationPerformed = false`.
- Any proof manifest lacks failing-first proof, semantic positive proof, source assertions, anti-stub audit, or changed-file hashes for a critical subbundle.
- Any UI/media/browser file changes.
