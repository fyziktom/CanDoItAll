# CanDoItAll Process / Workflow / Agent Hardening Follow-up Bundle V2

Status: **Completed / validated**
Profile: **initiative + post-implementation QA feedback**  
Prepared: `2026-06-02`  
Target repository: `fyziktom/CanDoItAll`  
Target branch: `development`  
Reviewed head commit: `629cf8addfb4eb883c1ca698642de90f4ccd4bdf`  
Compared against previous development commit: `154c61dbc82b1c2cdf64bae26cac5c138f7abeba`

## Purpose

Codex implemented and marked `codex/bundles/process-workflow-agent-hardening-v1` as completed. This follow-up bundle is a senior C# architecture and QA re-entry package for the next iteration. It focuses on the parts that were improved but are still too fragile before more processes, workflows, agents, skills, tools, MCP integrations, and application-generation flows are added.

## Executive Verdict

V1 materially improved the platform: canonical process operation names exist, durable provider usage observations were added, process cost aggregation is now ledger-first when usage observations exist, browser proof rules were strengthened, and five domain-distinct SB08 scenarios were added.

At preparation time, V1 was still **not sufficiently closed** for the next feature wave. The main remaining problems were not cosmetic:

1. A governed process step can still become fail-open when its allowed-operation contract is absent or incomplete.
2. Tool registration/classification is not yet one canonical table. Several known tool names can avoid explicit metadata and fall back to `Read` classification.
3. Token/cost accounting is better, but raw provider usage normalization is incomplete for OpenAI-style details such as cached tokens, reasoning tokens, total tokens, usage-null states, background polling, and finalizer short-circuit paths.
4. The SB08 “five process E2E” proof is useful, but it is not a real agent-driven app-creation process test. The harness starts process runs, then generates apps locally, manually transitions steps with automation dispatch suppressed, and explicitly records no CanDoItAll provider execution runs.
5. The proof validator accepted closure even though the critical E2E proof bypassed the exact automation path the user wanted to validate.
6. Large policy/dispatch services remain heavily heuristic and need decomposition before more process families are added.

## Completion Result

This V2 follow-up bundle has been implemented and validated. Final closure proof is in `proof/SB09/manifest.md`; completed-stage validation passed in `proof/SB09/transcripts/completed-validation.txt`.

## Non-negotiable Follow-up Principles

1. **Fail closed for governed process operations.** Missing allowed operations must never mean “let the tool proceed”.
2. **One tool registry.** Every known tool id must have explicit classification, operation requirements, side-effect semantics, approval default, and test coverage.
3. **Provider usage observations are the accounting source of truth.** Legacy metrics must be derived or fallback-only, not an independent competing total.
4. **No fake E2E.** Real process E2E means automation dispatch is active, agent execution runs exist, tool receipts exist, usage observations exist when a provider call happens, and app code is produced by the process path rather than by the proof harness.
5. **Proof validators must catch proof-path bypasses.** A critical proof that manually seeds production-only signals, suppresses automation, or has empty execution runs must fail closure unless the subbundle explicitly says it is only a fixture/backfill/migration test.
6. **Refactor only after gates protect behavior.** Split monoliths after SB01-SB05 have failing-first and passing proof.

## Subbundle Index

| ID | Name | Critical? | Summary |
| --- | --- | --- | --- |
| SB01 | Fail-closed process operation contracts | Yes | Make missing/incomplete process operation contracts explicit blockers for governed live runs and migrate templates. |
| SB02 | Canonical tool capability registry and policy decomposition | Yes | Replace split tool catalog/metadata/default-read behavior with one canonical registry and smaller policy services. |
| SB03 | Provider usage normalization and billing reconciliation | Yes | Normalize provider raw usage, preserve reasoning/cache/total fields, and reconcile with OpenAI billing/export evidence. |
| SB04 | Real agent-driven multi-domain process E2E harness | Yes | Replace SB08 proof gap with real automation-dispatch app-generation runs for five domain-distinct scenarios. |
| SB05 | Proof-quality anti-fake gates | Yes | Add validators that fail manual-transition, no-provider-run, harness-generated-code, and stale/fake proof cases. |
| SB06 | Process dispatch heuristic refactor | Yes | Extract required-tool, browser-proof, artifact, and completion heuristics into deterministic services with typed contracts. |
| SB07 | Agent/template/skill governance resync | Yes | Update templates, agents, skills, and active skill-root hashes for the stricter contracts and proof rules. |
| SB08 | UI and observability hardening for blockers/usage | No | Make contract blockers, unknown usage, and deny reasons visible and non-misleading in UI. |
| SB09 | Final senior QA red-team and release gate | Yes | Re-run the gate as a hostile reviewer, including fake-proof, billing, registry drift, and process E2E adversarial checks. |

## Readiness Gate

This prepared bundle must pass local structural validation before Codex starts implementation:

```powershell
python scripts/validate_bundle.py --stage prepared
```

Then Codex must run the repository skill gate:

```text
candoitall-bundle-validator
```

## Primary Current-State Evidence

See `analysis/01-current-state-review.md` and `evidence/01-reviewed-source-evidence.md` for the reviewed source observations. This bundle intentionally preserves the V1 proof contradiction: V1’s reports claim SB08 is a five-scenario process E2E, but the SB08 script itself records manual transitions, automation suppression, generated app code in the harness, empty agent execution runs, and no provider usage.
