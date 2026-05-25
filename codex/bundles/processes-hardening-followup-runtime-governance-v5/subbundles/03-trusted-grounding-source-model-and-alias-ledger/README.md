# SB03 - Trusted Grounding Source Model and Alias Ledger

## Status


- Completed

## Objective

Replace broad text-scraped external alias grants with a typed grounded-target ledger.

## Covered Inputs

- RQ03
- VF04
- N004
- N005

## Prerequisites

- SB01 and SB02 closure gates passed.
- Operation-aware target mutability decisions are available to grounding resolution.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs
- repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Scope

- Typed grounded-target alias records with source kind, intended use, trust level, confidence, and scope.
- Writable product targets granted only from trusted launch/project-structure sources and compatible operation contracts.
- Artifact summaries/provenance retained as read context, not write authority.

## Dependency Impact

- Critical subbundle.
- SB04-SB08 depend on trusted aliases for artifact validation and invariant audits.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add red-team tests for stale sibling paths in artifact summaries.
- Introduce typed grounding records and resolve write/read aliases from those records.
- Persist or journal ledger data where the runtime can audit it.
- Update proof artifacts after focused grounding tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB03/.

## Acceptance Checklist

- [ ] Old sibling path in upstream summary does not become writable.
- [ ] Current project-structure target becomes read-only or writable according to operation contract.
- [ ] No-go/exclusion paths never become targets.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB03/manifest.md
- proof/SB03/semantic-invariants.md
- proof/SB03/transcripts/failing-first.txt
- proof/SB03/transcripts/passing.txt
- proof/SB03/transcripts/source-assertions.txt
- proof/SB03/transcripts/anti-stub-audit.txt
- proof/SB03/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- N/A unless ledger inspection UI is added.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB03 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
