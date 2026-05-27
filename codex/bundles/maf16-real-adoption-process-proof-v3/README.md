# MAF 1.6 Real Adoption + Process Proof v3

## Status

Prepared for Codex execution.

## Why this bundle exists

The previous bundle was completed and the branch now contains:

- MAF 1.6 package references.
- A feature adoption matrix.
- stronger process artifact validation.
- live-run profiles and process-template improvements.
- reported build/test/web smoke proof.

However, before a real live test, we need one more review-hardening pass:

1. prove that MAF 1.6 features are actually used where useful, not only documented;
2. verify deferred features with runtime reflection, not grep-only assumptions;
3. fix remaining process artifact correctness risks;
4. create a safe preflight gate for the next real Blazor/Tetris live process run.

## Review observations this bundle starts from

### MAF package state

`CanDoItAll.AgentFramework.Maf.csproj` references:

- `Microsoft.Agents.AI` `1.6.2`
- `Microsoft.Agents.AI.A2A` `1.6.2-preview.260521.1`
- `Microsoft.Agents.AI.OpenAI` `1.6.2`
- `Microsoft.Agents.AI.Workflows` `1.6.2`

`CanDoItAll.AgentFramework.Hosting.csproj` references:

- `Microsoft.Agents.AI.Hosting.A2A` `1.6.2-preview.260521.1`

This proves the package upgrade, but not full feature adoption.

### MAF 1.6 adoption state

The execution report says that:

- runtime context injection is adopted through `MessageAIContextProvider`;
- finalizer structured output remains explicit finalizer instructions/validation;
- `AgentSessionFiles` was not found and session file support remains deferred;
- A2A/handoff has deterministic smoke proof;
- workflow evaluation expected outputs are deferred to process/workflow assertions.

That can be a valid engineering choice, but it needs a stronger proof boundary before live tests.

### Process runtime concern

`ProcessesService.RecordArtifactAsync` currently queries existing artifacts by:

- `ProcessRunId + ProjectionIdentityHash`
- `ProcessRunId + ExternalReferenceKey`

The reviewed source returns the existing artifact id immediately. If the identity or external reference collides across steps/expectations in the same run, this can incorrectly bind a required artifact to the wrong step or expectation. Codex's previous report claims this was solved, so this bundle requires proof and, if necessary, a fix.

### Narrative artifact concern

`ProcessCompletionArtifactValidator.RequiresManagedEvidencePath` does not require stored content for Narrative/Decision modes. That may be fine for some manual decisions, but a strict first-step delivery contract should be content-backed and current-run evidence. This bundle requires an explicit content policy for required narrative artifacts rather than relying on implicit mode defaults.
