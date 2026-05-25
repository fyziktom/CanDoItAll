# SB02 - Operation-Aware Tool Policy

## Status


- Completed

## Objective

Make tool policy enforce allowed operations, not only ProcessAllowsProductMutation.

## Covered Inputs

- RQ02
- VF03
- N004

## Prerequisites

- SB01 closure gate passed.
- agentProcessStepAllowedOperations and agentProcessStepTargetScope metadata are trusted from persisted contracts.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs
- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://src/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs

## Scope

- Operation-class parsing from process metadata in policy context.
- Generic tool-to-operation mapping for validation, runtime launch, proof capture, external action, artifact write, and product mutation tools.
- Policy denials for disallowed operation classes while preserving current-run artifact writes.

## Dependency Impact

- Critical subbundle.
- SB03 target grounding and SB08 invariant audit rely on the same operation taxonomy.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add failing tests showing non-mutating steps can invoke validation/runtime tools under old product-mutation-only checks.
- Extend policy context and metadata resolution with allowed operations and target scope.
- Map known tool classifications to required process operations and deny unsupported operation classes.
- Update proof artifacts after focused unit tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB02/.

## Acceptance Checklist

- [ ] Architecture step cannot run product validation or launch runtime.
- [ ] Review step can read and validate only if RunValidation is allowed.
- [ ] Artifact-only step can write current-run process artifacts.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB02/manifest.md
- proof/SB02/semantic-invariants.md
- proof/SB02/transcripts/failing-first.txt
- proof/SB02/transcripts/passing.txt
- proof/SB02/transcripts/source-assertions.txt
- proof/SB02/transcripts/anti-stub-audit.txt
- proof/SB02/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- N/A unless tool-policy state becomes browser-visible.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB02 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
