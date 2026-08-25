# SB06 — Shared connector, effective runtime projection, and hybrid provider use

State: `PASS`
Proof tier: `Governed`
Depends on: `SB04, SB05`
Next on pass: `SB07`

## Objective

Integrate imported profiles into existing Workspace/AgentFramework/MAF provider selection as a shared connector while preserving personal providers and avoiding a second runtime.

## Observable outcome

Agents, simple chats, workflows, health, and image consumers can select imported shared profiles through the existing OpenAI-compatible runtime path.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Register provider.candoitall-shared connector manifest and basic Workspace adapter behavior.
- Implement effective runtime profile materializer from ProviderProfile + import + source.
- Map shared connector to ProviderKind.OpenAi with remote transport/purpose/capabilities and central routing model ID.
- Preserve connector/origin metadata and source/availability tags without secrets.
- Update WorkspaceAgentProviderProfileMapper/registry through the smallest outer adapter change.
- Implement source/publication availability gate before runtime use.
- Integrate health/test behavior through canonical shared source/inference services.
- Prove personal and shared profiles coexist and explicit selection is stable.
- Prove central outage/unpublish does not silently fall back.
- Decide and implement legacy Workspace execution as thin facade or explicit unsupported path.
- Add composition/architecture/runtime tests.
- Do not add ProviderKind.Shared to inner runtime unless SB00 produced a blocking reason and architecture gate approved it.

## Out of scope

- No new UI.
- No multi-instance Docker yet.
- No changes to agent/tool ownership.
- No automatic provider fallback.

## Implementation sequence

1. Keep source/import database lookup and materialization outside inner MAF projects.
2. Give inner runtime a complete effective profile using existing provider model.
3. Ensure OpenAI SDK base URI points to central/EGCP openai/v1 root and route model ID.
4. Set Responses versus Chat Completions from validated catalog.
5. Map image purpose to existing OpenAI image runtime path.
6. Derive capabilities/models from import snapshot and adapter support, not editable booleans.
7. Fail fast with typed unavailable/identity/auth errors before SDK dispatch.
8. Update commit observer/invalidation so sync changes refresh catalog once.
9. Add guardrail against runtime connector switches/outer references.
10. Exercise one ordinary agent/simple chat and image runtime path with deterministic local host.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Workspace/AgentFramework module owns effective projection. Inner MAF owns existing SDK runtime. Shared HTTP service owns central inference. Connector manifest identifies origin.

## Dependency Direction

No new inner MAF reference to Workspace/SharedProviders Http. AgentFramework module may consume Workspace materializer as an outer adapter.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Anti-corruption/effective-profile adapter and thin compatibility facade.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Pure materializer tests, composition smoke, and focused runtime integration with deterministic central host.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

Modify mapper minimally or extract a strategy/materializer; do not enlarge connector switch indefinitely.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Effective profile snapshots for text/image transports.
- Project/reference no-cycle evidence.
- Composition registration.
- Agent/simple-chat/workflow/image targeted invocation.
- Personal/shared coexistence.
- Unavailable/no-fallback behavior.
- Legacy path decision and proof.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderRuntimeProfileMaterializerTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderRuntimeProfileMaterializerTests` | 18 | Covers effective profile mapping, capabilities, transport, purpose and unavailable states. |
| `SharedProviderRuntimeProjectionIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderRuntimeProjectionIntegrationTests` | 16 | Covers catalog projection, composition and actual runtime invocation. |
| `SharedProviderHybridSelectionTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderHybridSelectionTests` | 10 | Covers personal/shared coexistence and no silent fallback. |

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

- Imported shared profile appears in the same provider catalog as personal profiles.
- Existing MAF OpenAI-compatible path performs the call.
- Connector origin remains visible as shared.
- Remote-owned capabilities are enforced.
- No ProviderKind.Shared spread or duplicate runtime.
- Outage/unpublish fails explicitly.

## Negative proof

- Missing import/source relationship cannot materialize.
- Unavailable/identity-mismatch source cannot invoke.
- Shared failure with personal provider present does not switch provider.
- Forged local capability flag cannot enable unsupported central feature.
- Architecture guardrail detects any inner reverse reference.

## Semantic invariants

- Shared connector is an origin adapter, not a second agent runtime.
- Provider selection remains explicit.
- Inner MAF dependency direction remains unchanged.

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
`SB07`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## August 25 downstream revalidation

SB07 relay repairs named SB04 wire-contract and capability/operation invalidation keys. The
historical SB06 authority resolved to Debug assemblies, so it was retained as chronology and the
unchanged frozen selections were listed and rerun with current Release assemblies. Materializer,
runtime projection, and hybrid selection pass 18/18, 16/16, and 10/10 respectively. No broad,
Playwright, multi-instance, application-image, live-provider, or paid-provider lane ran. Evidence
is recorded in
`proof/architecture/sb04-downstream-invalidation-release-revalidation.md`.

## Reopen triggers

- Current MAF SDK cannot use the central custom endpoint without inner changes.
- Image runtime mapping requires a purpose-specific strategy not captured by effective profile.
- Legacy Workspace path is a current production consumer requiring a broader facade.

## Execution checklist

- [x] Current branch/commit/worktree captured.
- [x] Mandatory skills loaded.
- [x] Bundle and subbundle readiness validated.
- [x] Dependencies are `DONE`.
- [x] Before architecture/reference evidence captured.
- [x] Scope implemented without widening.
- [x] Affected production projects built.
- [x] Test discovery recorded and nonzero.
- [x] Focused positive/negative tests passed.
- [x] Security/redaction checks passed where applicable.
- [x] After architecture/reference evidence captured.
- [x] Proof manifest completed with artifact hashes.
- [x] Session handoff completed.
- [x] Status/traceability/review updated.
