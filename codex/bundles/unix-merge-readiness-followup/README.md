# CanDoItAll Unix Adoption — Merge-Readiness Follow-up Bundle

Prepared for **Codex GPT-5.6 xhigh**.

## Mission

Turn the current `unix-adoption` branch into a clean, reproducible local merge candidate for Windows and Linux, then hand one immutable candidate to a colleague for genuine macOS validation.

This bundle is a **follow-up and hardening bundle**, not a rewrite of the completed Core and Runtime portability work. Preserve the architecture already introduced unless a listed invariant or regression proves it defective.

## Exact prepared anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `unix-adoption`
- Commit: `e282446daa2b775b93f2d70ea7fc0e282e26d802`
- Parent runtime-bundle commit: `246534dbaa1042627689716027b27ce959aa4220`
- Prepared on: `2026-08-12`
- Target model: `GPT-5.6 xhigh`

## Compatibility map

This compact initiative bundle preserves the architect's original shape. Its semantic roles are:

- source input: `ORIGINAL-REQUEST.md` and `review/00-executive-review.md`;
- requirements and closure ownership: `requirements/requirements.md`, `requirements/requirements.json`, and `plan/06-proof-and-progression-contract.md`;
- current state and source references: `review/`, `inventories/`, and `architecture/`;
- dependency plan and work units: `plan/` and `subbundles/`;
- execution status, proof, and closure: `review/03-execution-report.md`, subbundle `result.md` files, and `proof/`.

The canonical structural validator targets a different scaffold and is therefore advisory for this compatible shape. Manual readiness and closure gates use the same semantic contract.

## Validation Summary

- Prepared-anchor validation: re-anchored from `e282446daa2b775b93f2d70ea7fc0e282e26d802` to clean branch head `386d8beb6038035f89a9a6961ec017d8213879a5`; see `review/04-reanchor-report.md`.
- Current execution status: `M00-M08`, `C1`, and `C2` are locally complete. The M09 handoff is ready, but actual macOS arm64 execution has not occurred. M10 records the bounded final decision.
- Current decision: `NO-GO — actual macOS arm64 colleague validation is still required before MERGE READY`.

Before changing code, verify that the checkout still matches this anchor. If it does not, produce a bounded re-anchor report and remap every changed hotspot before implementation.

## Current disposition

The local implementation is a proven Windows/Linux candidate, but it is **not merge-ready yet** because genuine macOS evidence is absent. The three original P0 issues are closed:

1. backward-compatible persisted process-plan hash/capability migration;
2. reproducible FileTools direct-source provenance and truthful capability claims;
3. complete owned process-tree termination when the root exits before descendants.

The remaining gate is the exact M09 command sequence on an actual macOS arm64 colleague host.

## Accepted alpha deferrals

The following are explicitly out of scope for this bundle unless a regression requires touching them:

- complete Azure Key Vault and HashiCorp Vault implementations;
- upgrading the macOS Keychain adapter to a newer native API family;
- declaring macOS support verified before colleague-run actual-host evidence;
- GitHub-hosted CI execution before the local merge candidate is ready;
- general product refactors unrelated to Unix adoption.

The existing `LocalUserFile` `BasicLocal` fallback, Windows DPAPI, explicit external wrapping-key provider, and fail-closed strong-provider selection are sufficient for the alpha merge candidate.

## Execution order

Execute subbundles strictly in order:

1. `M00` — anchor, baseline, and repository hygiene
2. `M01` — persisted process-plan compatibility
3. `M02` — FileTools provenance and dependency mode
4. `M03` — process-tree ownership and termination
5. `C1` — shared checkpoint; one optional scheduled full suite
6. `M04` — local stdio MCP protocol hardening
7. `M05` — Docker recipe and local-stack hardening
8. `M06` — executable and workspace path authority hardening
9. `C2` — runtime portability checkpoint; no full suite
10. `M07` — validation tooling and canonical documentation
11. `M08` — integrated Windows/Linux merge-candidate gate
12. `M09` — macOS colleague handoff
13. `M10` — final bookkeeping and merge-readiness decision

## Validation rule

Do **not** run all 7,000+ tests after each edit or subbundle. Build once per coherent checkpoint, run affected tests with `--no-build --no-restore`, and run the full stable suite only at the explicitly named milestones in `plan/02-validation-strategy.md`.

## Safety rules

- Do not push, merge, or rewrite history without explicit operator instruction.
- Do not discard unrelated working-tree changes.
- Do not weaken fail-closed behavior to make a test pass.
- Do not silently regenerate portability baselines before reviewing the delta.
- Do not claim macOS actual-host support from cross-publish or Docker evidence.
- Do not expose secrets, full physical roots, environment dumps, or connection strings in evidence.
- Keep all source-code comments in English.
