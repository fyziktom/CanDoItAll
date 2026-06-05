# Current State

The current branch has made strong progress:

- `ProcessDispatchCandidateHeaderSelector` owns candidate header selection.
- `ProcessDispatchCandidateHydrationLoader` owns readback of the candidate hydration snapshot.
- `ProcessDispatchCandidateAssemblyContext` and `ProcessDispatchCandidateFactory` own route-specific `DispatchCandidate` construction.
- `ProcessDispatchTechnicalAgentBindingCoordinator` owns technical-agent binding and project-structure read-access mutation.
- `ProcessDispatchRecoveryQueryHelper` owns manual recovery directive and recoverable execution query access.
- `ProcessDispatchCooperationMetadataResolver` owns cooperation metadata and workspace tool profile classification.

Remaining issue: `ProcessRunAutomationDispatchService.Dispatch.cs` still owns a large pre-execution guard/materialization region. This region is side-effectful and should not be moved into a future core until its decisions and side effects are separated.

The next bundle should make local seams for:

- database requirement blocking,
- upstream artifact gap facts,
- downstream block transition request construction,
- materialization fingerprint/dedup,
- journal record coordinator,
- upstream rerun request builder,
- pre-execution guard orchestration wrapper.
