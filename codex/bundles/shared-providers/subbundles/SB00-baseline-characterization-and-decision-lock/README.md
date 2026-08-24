# SB00 — Baseline characterization and decision lock

State: `READY`  
Proof tier: `Governed`  
Depends on: `none`  
Next on pass: `SB01`

## Objective

Revalidate the current repositories, prove the real provider/runtime/API/usage/persistence paths, and lock the target dependency graph before feature code.

## Observable outcome

A current-state evidence pack and passing architecture gate remove all guessing about ownership, project references, provider call paths, and test surfaces.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Capture current branch, commit, working tree, .NET SDK, Docker/Compose, and sibling repository availability.
- Load all mandatory skills and record their current paths/hashes.
- Create a scoped CodeAnalytics snapshot and record project, namespace, type, and cycle evidence.
- Read every source anchor in inputs/02-source-artifacts-and-standards.md at the current commit.
- Trace provider create/edit/delete, commit observer projection, ordinary agent creation, simple chat, workflows, health, image generation, and legacy Workspace execution.
- Characterize existing provider usage observation/persistence and whether external relay traffic can be represented truthfully.
- Characterize provider reference/deletion checks and transaction conventions.
- Confirm which connector manifests are production-configurable, including Azure status.
- Confirm OpenAI SDK base URI, Responses, Chat Completions, Images, streaming, and custom endpoint behavior with narrow characterization tests where source inspection is insufficient.
- Confirm current API error/OpenAPI/SSE/auth conventions and current Compose build constraints.
- Record before project-reference table and preferred after graph; amend architecture docs only with evidence.
- Run the bundle validator and C# architecture review gate.

## Out of scope

- No shared-provider production behavior.
- No new EF entities or migration.
- No public routes.
- No UI.
- No broad test project or solution.

## Implementation sequence

1. Start from a clean evidence directory under this subbundle proof folder.
2. Use CodeAnalytics MCP when available; otherwise record why and use csproj/slnx/static namespace inspection.
3. Add only narrow characterization/architecture tests that protect an otherwise ambiguous foundation.
4. Update current-state, inventory, ADR, dependency, and test maps to current symbols.
5. Resolve every preparation assumption as Confirmed, Amended, or Blocked.
6. Write a checkpoint review with exact project shape and downstream reopen triggers.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Evidence and characterization only. No new product owner is introduced.

## Dependency Direction

No project-reference change is expected. If characterization needs a test reference, justify it and do not move production types.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Characterization tests plus architecture decision records; no speculative abstraction.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Tests target existing provider mapping/runtime/auth/usage behavior and architecture invariants. They must not call live providers.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

Do not add product partials. Characterization tests use new focused classes.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Current repository/skill baselines.
- Before dependency graph and no-cycle output.
- Runtime call-path diagram with symbol anchors.
- Connector and capability inventory.
- Usage/deletion/OpenAPI characterization.
- Focused test discovery and result.
- Architecture gate decision.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderArchitectureCharacterizationTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderArchitectureCharacterizationTests` | 8 | Protects current ownership, connector registry, and dependency assumptions. |
| `SharedProviderRuntimePathCharacterizationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderRuntimePathCharacterizationTests` | 6 | Proves current effective provider mapping and custom endpoint behavior without live providers. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## Acceptance criteria

- Every prepared assumption is classified.
- Target project/ownership graph is executable without a cycle.
- No unresolved question remains about canonical provider persistence or runtime invocation.
- Azure/audio scope is decided from current production evidence.
- One exact SB01 contract/project plan is unlocked.

## Negative proof

- Guardrail proves inner MAF projects do not reference Workspace/Web/EF.
- Search proves internal ProviderProfile/request records are not current public API DTOs.
- Zero or ambiguous test discovery fails the gate.

## Semantic invariants

- Workspace EF provider row is the canonical master.
- Inner provider/runtime projects gain no outer reference.
- No feature implementation begins before the decision lock.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`SB01`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Provider projects or module layout changed since evidence capture.
- Current branch adds an existing shared-provider feature.
- Provider usage or API authorization ownership moved.
- A project cycle appears in the preferred graph.

## Execution checklist

- [ ] Current branch/commit/worktree captured.
- [ ] Mandatory skills loaded.
- [ ] Bundle and subbundle readiness validated.
- [ ] Dependencies are `DONE`.
- [ ] Before architecture/reference evidence captured.
- [ ] Scope implemented without widening.
- [ ] Affected production projects built.
- [ ] Test discovery recorded and nonzero.
- [ ] Focused positive/negative tests passed.
- [ ] Security/redaction checks passed where applicable.
- [ ] After architecture/reference evidence captured.
- [ ] Proof manifest completed with artifact hashes.
- [ ] Session handoff completed.
- [ ] Status/traceability/review updated.
