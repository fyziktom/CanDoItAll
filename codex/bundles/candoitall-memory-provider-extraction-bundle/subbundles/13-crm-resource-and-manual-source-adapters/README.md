# 13 Crm Resource And Manual Source Adapters

## Status

- `Completed`

## Objective

- Add Source Gateway adapters for CRM, Resources, manually supplied text/files/links, and generic future source registration.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R07
- R08

## Prerequisites

- SB10 gate passed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `repo://src/Modules/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemoryExternalSourceIngestionPolicy.cs`
- `repo://src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions/MemorySourceSnapshotModels.cs`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement Source Gateway adapters for CRM, HR/client/account records, Resources, manual text/file/link ingestion, and future-source registration points.
- Define source kinds and redaction policies for customer data, resource files, uploaded content, and manually supplied notes.
- Support provider-initiated source requests for allowed CRM/resource scopes and user-initiated manual ingestion jobs.
- Add future source registration APIs so new modules can add adapters without editing provider drivers.
- Add tests for sensitive CRM redaction, resource metadata snapshots, manual text ingestion, manual file-reference ingestion, and denied scopes.
- Keep future source registration compatible with the existing `MemorySourceSnapshot*` contract family or its explicitly migrated replacement.

## Dependency Impact

- Provider ingestion and manual UI actions depend on consistent adapter behavior.

## Validation Depth

- `Source adapter foundation`

## Implementation Steps

1. Review CRM/HR and Resources module boundaries and identify safe read abstractions or adapter facades.
2. Implement snapshot mappers with sensitivity classification and redaction before payload leaves the source module boundary.
3. Implement manual ingestion request model for text, file ref, link, source category, tags, and selected provider.
4. Add adapter registration extension points and tests with a fake future module adapter.
5. Document source-kind ids and retention/sensitivity defaults.
6. Add tests proving future module adapters cannot bypass Source Gateway policy or introduce incompatible source snapshot DTOs.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- Sensitive CRM data is redacted or denied according to policy before provider delivery.
- Manual ingestion uses the same operation/source/feedback ledger as provider-requested ingestion.
- Future modules can register a source adapter without modifying generic provider drivers.

## Proof Required

- Create `proof/SB13/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run CRM/resource/manual adapter tests including sensitivity and redaction cases.
- Run source audit proving manual ingestion still records provider id, source snapshot id, and operation id.
- Run source contract audit proving CRM/resource/manual/future-source adapters use the SB04 source snapshot contracts.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start because SB13 proof is recorded, the acceptance checklist passed, and no phase-gate blocker remains.

## Closure Evidence

- Proof manifest: `bundle://proof/SB13/manifest.md`
- Semantic invariants: `bundle://proof/SB13/semantic-invariants.md`
- Focused CRM/resource/manual tests, source adapter regressions, Workbench integration smoke, full memory suite, source audits, anti-stub audit, and solution build all passed in `bundle://proof/SB13/transcripts/`.
- Browser validation: `N/A`; this subbundle changed no browser-visible surface.

## Suggested Agent Prompt

```text
Implement subbundle SB13 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
