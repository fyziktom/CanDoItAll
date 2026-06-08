# Release Gate Matrix

| Area | Release gate | Current result |
| --- | --- | --- |
| Process Core | No references to driver abstractions, verifier packages, modules, infrastructure, storage, workspace, or runtime services. | Covered by source scans and architecture tests. |
| Driver abstractions | Contract-only package; no package references, project references, provider abstractions, registry, host, selector, or runtime registration. | Covered by `ProcessDriverContractApiVerificationBoundaryTests`. |
| Transcript verifier alpha | Reads supplied transcript text and evidence references only; emits diagnostics, audit facts, redaction, evidence references, and no-mutation proof. | Covered by `ProcessDriverTranscriptVerificationAlphaTests`. |
| Process module adapter | Single narrow read-only adapter; no generic runtime registration, scheduler hook, workflow hook, manager command, external call, file read, storage write, workspace write, or process mutation. | Covered by `ProcessTranscriptVerificationReadOnlyAdapterTests`. |
| Evidence payload policy | Transcript hash is validated before verifier invocation; unsafe URI schemes and invalid hashes deny explicitly. | Covered by hash mismatch and untrusted source integration tests. |
| Domain denial lanes | Office and business-analysis lanes remain read-only proposal lanes and cannot use the `.NET/Rust` verifier path. | Covered by non-`.NET/Rust` lane denial integration tests. |
| Runtime host | Still deferred until audit persistence, capability policy, sandboxing, timeout, approval, and ownership proof exist. | Deferred by architecture docs and source scans. |

## Final Gate
- Build: `dotnet build CanDoItAll.slnx --no-restore`
- Unit: `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore`
- Focused integration: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter FullyQualifiedName~ProcessTranscriptVerificationReadOnlyAdapterTests`
- Source scans: forbidden runtime/DI/driver hooks in the adapter and no driver references in Core.
- Bundle validators: prepared and completed stages.
