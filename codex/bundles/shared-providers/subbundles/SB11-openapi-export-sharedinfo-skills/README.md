# SB11 — OpenAPI freeze, export, SharedInfo snapshot, manifest, and API skill

State: `LOCKED`  
Proof tier: `Governed`  
Depends on: `SB10`  
Next on pass: `SB12`

## Objective

Freeze and publish the exact shared-provider HTTP contract into OpenAPI and CanDoItAll.SharedInfo with current provenance, route parity, and usable API guidance.

## Observable outcome

Live Web OpenAPI, SharedInfo snapshot/manifest, and the new shared-provider skill agree exactly and validators pass.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Run focused OpenAPI integration tests for route/method/schema/auth/error presence.
- Verify no audio/management/unsupported route is exposed.
- Start a clean final Web host and capture both OpenAPI JSON endpoints.
- Prove required byte identity and compute SHA-256/counts.
- Update _candoitall-api-shared snapshot, manifest and README with final provenance.
- Add documented operation set for shared providers.
- Create candoitall-api-shared-providers/SKILL.md and focused references.
- Add route appendix parity markers.
- Update current skill indexes/install/generation/validation surfaces.
- Synchronize active skill copies if current workflow requires it.
- Run SharedInfo and OpenAPI validators.
- Scan for stale branch/commit/route/capability wording.

## Out of scope

- No new product feature.
- No final stable aggregate.
- No final multi-instance run.
- No unsupported compatibility claim.

## Implementation sequence

1. Capture only after route/schema freeze.
2. Use live OpenAPI as exact schema source; do not hand-edit the snapshot.
3. Record final CanDoItAll commit and dirty state accurately.
4. Follow existing API skill structure and route appendix comments.
5. Explain native catalog versus OpenAI-compatible error envelopes.
6. Explain catalog/invoke scopes, access-context semantics, tool ownership, images, streaming and denied features.
7. Prefer live target OpenAPI when source version differs.
8. Run validators once after all edits are complete; fix narrowly.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

CanDoItAll Web owns live spec. SharedInfo owns copied evidence and skills.

## Dependency Direction

No product ProjectReference change. SharedInfo skill links to shared snapshot.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Generated contract snapshot plus human operational skill; single source live OpenAPI.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Focused API documentation integration and SharedInfo parity/hash validators.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

No production partials.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- OpenAPI focused tests.
- Two endpoint hashes/byte comparison.
- Shared snapshot SHA/count/provenance.
- Operation set route list.
- Skill route parity and front matter.
- SharedInfo validator transcripts.
- Stale/unsupported claim scan.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderOpenApiIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderOpenApiIntegrationTests` | 10 | Proves final route/schema/auth/error surface before capture. |
| `SharedInfoValidation` | `CanDoItAll.SharedInfo/tools/validation` | `OpenAPI, route parity, skill manifest, repository validation` | 4 | Proves snapshot provenance and skill synchronization. |

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

- All five intended operations are present with correct methods.
- Unsupported routes absent.
- Snapshot and manifest match final host.
- New skill is discoverable and route-parity-valid.
- SharedInfo validators pass.

## Negative proof

- No raw secret/internal profile schema appears in OpenAPI.
- No audio/full OpenAI/EGCP administration claim.
- No stale simple-chat branch provenance.
- No manual snapshot divergence.

## Semantic invariants

- Live OpenAPI is the schema source.
- SharedInfo describes only implemented tested behavior.
- Snapshot provenance is final and reproducible.

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
`SB12`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Any product API changes after capture.
- SharedInfo schema/validator convention changed.
- Route parity or OpenAPI hash fails.

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
