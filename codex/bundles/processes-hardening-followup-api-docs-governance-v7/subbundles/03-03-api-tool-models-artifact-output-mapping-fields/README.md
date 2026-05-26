# SB03: api-tool-models-artifact-output-mapping-fields

## Status

- Status: Completed

## Objective

Artifact output mapping and projection lineage fields survive nested API routes.

## Covered Inputs

- bundle://inputs/02-structured-input.md
- bundle://requirements/01-normalized-requirements.md
- bundle://traceability/01-requirement-traceability.md

## Prerequisites

- Prepared-stage bundle validator passed before production edits.
- Upstream phase6 process hardening source was preserved unless this subbundle exposed schema drift.

## Exact Source References

- repo://src/CanDoItAll.Web/Api/ProcessesApi.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Reads.cs
- repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs

## Deliverables

- Closed typed process API/read-model drift for the fields owned by this subbundle.
- Preserved existing source-backed behavior for subbundle requirements that phase6 already implemented.
- Updated proof manifest: proof/SB03/manifest.md.

## Dependency Impact

- Downstream process API, runtime tooling, artifact lineage, recovery health, and final closure gates were rechecked through the focused validation transcript.

## Validation Depth

- Source assertions, adversarial negative proof, semantic positive proof, anti-stub audit, changed-file hashes, and focused tests are recorded under bundle://proof/SB03/.

## Implementation Steps

1. Confirmed source references still resolve.
2. Fixed the shared API/read-model contract drift where required.
3. Added HTTP-level regression coverage for nested process routes and JSON read-model fields.
4. Ran focused unit, integration, component, build, and source-audit validation.
5. Updated semantic proof and closure report.

## Do Not Do

- Do not add SQLite runtime paths or migrations.
- Do not replace typed contracts with display-string inference.
- Do not narrow process behavior to a Blazor-only or software-only scenario.

## Acceptance Checklist

- Focused tests pass through bundle://proof/SB16/transcripts/passing.txt.
- No stub-only implementation remains in the changed contract path.
- PostgreSQL-only audit was run and recorded.
- Public API/read-model fields remain synchronized with runtime request models.

## Proof Required

- bundle://proof/SB03/manifest.md
- bundle://proof/SB03/semantic-invariants.md
- bundle://proof/SB03/transcripts/failing-first.txt
- bundle://proof/SB03/transcripts/passing.txt
- bundle://proof/SB03/transcripts/source-assertions.txt
- bundle://proof/SB03/transcripts/anti-stub-audit.txt
- bundle://proof/SB03/transcripts/changed-file-hashes.txt
- reviews/01-execution-report.md includes the SB03 gate row.

## Browser Validation Logging

- Not required for this API/read-model-only implementation. API JSON evidence is in the ApiIntegrationTests regression transcript.

## Progression Gate

- Passed after source assertions, semantic proof, anti-stub audit, changed-file hashes, and validation commands agreed.

## Suggested Agent Prompt

SB03 is closed. Reopen only if the cited source references or validation transcripts no longer prove the typed process runtime contract.
