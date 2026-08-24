# SB10 — Operator documentation, Compose tooling, troubleshooting, and handoff flow

State: `LOCKED`  
Proof tier: `Behavioral`  
Depends on: `SB09`  
Next on pass: `SB11`

## Objective

Turn the proven implementation into a repeatable, documented operator/developer workflow and harden the E2E tooling for final leave-running handoff.

## Observable outcome

A developer can configure central/client sharing, run/reset/test the three-instance stack, troubleshoot failures, and later clean it up without reading implementation code.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Write product architecture/user documentation.
- Write central administrator and client source/import workflows.
- Write API/security/access-context/compatibility-limit documentation.
- Write multi-instance E2E runbook and troubleshooting matrix.
- Finalize Compose file, .env example, ignored artifact roots, start/run/stop/reset scripts and E2E orchestrator UX.
- Document token scope issuance and safe secret references without sample real secrets.
- Document reverse proxy/base path/TLS/private network policy.
- Document availability/sync/unpublish/source identity/outage semantics.
- Document usage/audit content exclusions and retention direction.
- Update docs indexes and docs/testing.md focused lane guidance.
- Validate scripts help/dry-run and documentation links.
- Prepare manual-handoff generator used by SB12.

## Out of scope

- No public API schema change unless a documentation-discovered defect reopens owner.
- No final OpenAPI capture.
- No stable aggregate.
- Do not stop a final stack because final stack is not yet SB12.

## Implementation sequence

1. Use copy-pasteable cross-platform commands and exact tracked paths.
2. Keep generated secrets in ignored files and output only locations.
3. Make start/run/stop/reset actions explicit; reset requires confirmation/safe target checks.
4. Document one app image reused three times.
5. Include expected healthy service names/ports with override mechanism.
6. Document how future EGCP fits without claiming it exists.
7. Document the bounded OpenAI subset and denied features prominently.
8. Run markdown/link/script validators and dry-run.
9. Do not duplicate OpenAPI schemas in prose; link live/snapshot.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Product docs live in CanDoItAll. SharedInfo API guidance remains SB11. Tools/Test Support remain non-production.

## Dependency Direction

No new product reference expected. Tool project references remain isolated from product graph.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Executable runbook and safe command-line orchestration.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Script `--help`, config validation, dry-run, safe reset refusal and docs link checks.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

No new production partials.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Documentation file/index inventory.
- Script help/dry-run/safe reset evidence.
- No secret in examples.
- Docs/testing focused lane update.
- Manual handoff generation preview.
- Cross-platform path/shell review.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderToolingValidation` | `tools/SharedProviders` | `help,dry-run,safe-reset` | 6 | Validates operator tooling without starting the final lane. |
| `DocumentationValidation` | `repository documentation validators` | `shared-providers docs/index/links` | 4 | Validates documentation structure, links and prohibited secret patterns. |

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

- Fresh operator can understand and run the topology.
- Compatibility/security limits are explicit.
- Cleanup is separate and deliberate.
- Manual handoff template contains locations but no values.
- Documentation validators pass.

## Negative proof

- Reset refuses non-E2E target.
- Examples contain no token/password/API key.
- Docs do not claim audio/full OpenAI/EGCP support.
- Start script does not rebuild three app images independently.

## Semantic invariants

- Operator tooling is safe and repeatable.
- Examples never contain real secrets.
- Documentation matches the tested subset.

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
`SB11`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- UI/API names changed after docs.
- Scripts require platform-specific assumptions not documented.
- Final SB12 discovers non-repeatable setup.

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
