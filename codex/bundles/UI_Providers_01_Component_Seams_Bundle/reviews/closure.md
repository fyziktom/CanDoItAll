# SB09 and Providers-01 closure

Implemented and validated on 2026-09-05 from components-decoupling at d3ba280a431bfe74ce03a72638ac06dff47de660. The authorized source work is complete. Repository-wide documentation validation is still blocked by 118 historical tracked logs already present in that baseline; this is not an unconditional merge-ready claim.

## Delivered behavior

Agents SB09 separates initial task-slot ownership, catalog generation and presentation loading. A newer reload accepted while the initial read is pending ends loading, and late initial success/failure cannot overwrite it. All three direct nested editor dialogs receive the actual editor-session token. Disposal or replacement cancels owned nested dialogs and preserves an unrelated dialog; no global CloseAll was introduced.

Providers-01 adds typed section/state definitions, one explicitly constructed per-panel session and one cohesive read boundary. The session owns selected identity, draft/EditContext, core load state, cancellation and stale-result checks. Core failures hide the form and Retry keeps the target. Secret-catalog failure is explicit and preserves an unavailable saved reference. Pending reads cannot reselect a superseded or removed provider. Section changes preserve context; metadata-only refresh of New preserves raw draft text.

The existing panel delegates reads to this owner while retaining save/delete, health, pricing refresh, shared-provider backend and request-history internals. Existing lazy History/form boundaries, shared connections and source-managed read-only behavior remain covered. No routing, project/package edge, physical extraction or sibling source changed. The [shared feedback](../../UI_Component_Seams_Shared_Architecture_Bundle/reviews/03-lifecycle-and-provider-followup.md) and program sequence are updated.

## Validation

- Added 51 cases: eight meaningful failing-first SB09 regressions, 27 provider Unit cases and 16 provider Component cases. All pass after the documented applicable repairs/reruns.
- Direct changed production-project builds passed, including the final module build with zero warnings/errors. The Web browser host was built and its AgentFramework assembly hash matched the directly built module.
- Conservative impact analysis explicitly requested all three supplied stable test lanes. Unit: 6864/6864; Integration: 1320/1320. Components: 1277/1278 initially, then the exact unchanged workflow-preview timeout passed its focused rerun. Total: 9462 executed, zero skipped. The original failure is retained; the broad run was not uninterrupted green.
- All 27 provider Unit cases and the six finally affected Component cases passed again after the last target-heading/New-refresh refinement. The 28-case owning provider Component selection is covered by its unaffected passing remainder and applicable reruns. Discovery expansions and invalidation are explained in [validation scope](validation-scope.md).
- Final portability scan included new untracked source. Enforcement passed without baseline edits: 14251 reviewed findings unchanged. Checker self-tests passed.
- Real browser at 1600x1000 verified provider load, all six sections, context/draft retention, separate History form, filter/reset, New refresh and raw model text, lazy Connections open/close, and Agents nested wizard/parent close. Final normal/overlay screenshots were inspected. The owned runtime was stopped and browser released to about:blank. Deterministic cancellation/race proof remains in component tests.
- Scoped architecture review found no new project dependency, cycle or blocking finding. The existing panel command/presentation remainder and two baseline cycles are explicitly retained as follow-up context.

Full logs, TRX and screenshots remain local ignored artifacts. Compact hashes, results and source inventory are in [final evidence](final-evidence.json), [test artifacts](test-artifact-summary.json), [browser evidence](browser-evidence.json) and [architecture review](csharp-architecture-gate.md). Delivered manifests omit the old ignored unpacked proof caches. Historical original-bundle proof is preserved.

## Remaining gate and next work

Test-Documentation.ps1 reports the same 118 tracked historical .log files as the baseline. No tracked log was added, removed or hidden, and no validator/baseline was weakened. Resolve that repository artifact/history issue before claiming full repository/merge closure.

The next bounded child is PROVIDERS-02, prepared around the registry's actual commit boundary. It must handle mutation outcomes, first-save identity, post-commit reconciliation, draft preservation and owned effects; current read hardening does not settle those contracts. Then follow the small AgentCatalogPanel UI assembly/catalog sandbox/watch measurement checkpoint, followed by independent provider history work. See [handoff](../handoff.md). None of those later children was executed here.
