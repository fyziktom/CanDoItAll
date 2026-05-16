# 02 Redaction And Hash Policy

## Status

- Completed

## Objective

- Harden source redaction, sensitivity, and hash classification so future durable memory and projections cannot treat restricted source data as unrestricted content.

## Covered Inputs

- H-FR-004, H-FR-005, H-NFR-002, H-NFR-003, and H-NFR-005.
- Raw notes: Workbench snapshots are not redaction-aware; sensitive raw payloads are included in source hashes.

## Prerequisites

- `01-source-paging-and-cursor-contracts` must close or explicitly state that redaction changes are independent from cursor changes.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Sources\MemorySourceSnapshotContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\WorkbenchProjectStructureSourceSnapshotProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeEvidenceSourceProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\WorkflowRuntimeEvidenceSourceProvider.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkbenchSourceSnapshotIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\RuntimeEvidenceSourceIntegrationTests.cs

## Deliverables

- Explicit hash classification or restricted integrity hash contract.
- Workbench sensitivity/access rules for notes, metadata JSON, storage locators, and future usage summary.
- Process and Workflow hash policy that separates exposed redacted content from restricted raw-payload integrity.
- Tests proving common sensitive values do not appear in exposed content, metadata intended for projection, or browser-visible trace fields.

## Dependency Impact

- Cognitive Memory canonicalization, Qdrant payloads, recall context packs, and review UI depend on this policy.
- Weak proof here can cause future vector projection or agent context leakage.

## Validation Depth

- Critical security and projection foundation.
- Integration tests must cover Workbench, Process runtime, and Workflow runtime samples.
- Source review must confirm restricted hashes are not used as general metadata.

## Implementation Steps

- Add typed hash usage/classification contract if current `ContentHash` is insufficient.
- Update providers to expose redacted content and explicit restricted/raw integrity metadata separately.
- Mark Workbench note-bearing items with accurate access mode and sensitivity.
- Add tests for Workbench notes, metadata JSON, storage references, Process payloads, Workflow payloads, and hash classification.
- Update QA prompt/execution report with remaining policy exceptions if any.

## Scope Exceptions

- Do not build the final Cognitive Memory redaction engine here.
- Do not scan every possible existing free-text field in the whole solution; cover the source boundary fields this bundle owns.

## Do Not Do

- Do not project or embed any source content.
- Do not store restricted hashes in Qdrant payloads.
- Do not claim `ContainsSensitivePayload: false` for Workbench text just because the source is project metadata.
- Do not remove provenance to avoid redaction work.

## Acceptance Checklist

- Exposed content is redacted where policy says it must be.
- Workbench notes and metadata carry accurate sensitivity/access metadata.
- Raw sensitive payload hashes are classified and not treated as unrestricted exportable metadata.
- Existing source snapshot tests still pass.

## Proof Required

- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~WorkbenchSourceSnapshotIntegrationTests|FullyQualifiedName~RuntimeEvidenceSourceIntegrationTests" --no-restore`
- Source review notes for hash classification and redaction decisions.
- Execution report rows for each raw note closed by this subbundle.

## Browser Validation Logging

- No browser proof is required unless implementation unexpectedly changes visible UI.
- If UI changes occur, record route, viewport, Playwright evidence, and screenshot in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to closure only after redaction/hash semantics are explicit enough for future Cognitive Memory projection and context-pack rendering.

## Suggested Agent Prompt

- Implement redaction and hash-policy hardening for the source boundary only. Keep content source-grounded, classify restricted hashes, update tests, and do not implement Cognitive Memory projection or recall.
