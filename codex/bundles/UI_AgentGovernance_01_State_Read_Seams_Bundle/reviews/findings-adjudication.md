# Candidate-risk adjudication before future implementation

Source inspected after Overview closure; no Governance RED or implementation executed. Line references below describe the hashed preparation source, not guaranteed future positions.

| Candidate | Adjudication from current source | Future witness / resolution |
|---|---|---|
| 1. Agent A -> B late list/detail | **Partly already guarded.** RefreshRunsAsync checks agentSelectionGeneration before accepting list/automatic detail. Catalog LoadAsync, outer errors and callbacks have no equivalent ownership. | Preserve existing successful guard; public A/B late failure/catalog/callback tests before changing it. |
| 2. Run R1 -> R2 late detail | **Confirmed unguarded.** SelectRunAsync assigns selectedDetail after await without run identity. | R1 success/failure after R2; independent detail generation. |
| 3. SelectRun request/token | **Confirmed.** No token, requested agent capture, row membership or returned detail identity validation. | Token plus Run.Id/AgentId validation against captured accepted row/target. |
| 4. Disposal pending reads | **Confirmed.** No disposal/CTS in the panel; all three reads omit token. | Public removal with cooperative and noncooperative completions. |
| 5. Old finally clears newer busy | **Confirmed.** LoadAsync/SelectRunAsync set shared isBusy=false unconditionally. | Older detail/catalog completion while newer read remains pending. |
| 6. Refresh overrides manual choice | **Confirmed.** Refresh captures runId, waits for detail, then writes selectedRunId; manual selection changes no agent generation. | Capture separate manual-selection revision; choose detail only for current accepted row. |
| 7. List success/detail failure | **Confirmed coupling.** runs is assigned only after detail succeeds (except empty result). | Accept list independently; explicit detail error/retry. Same-target stale list stays marked; cross-target list hidden. |
| 8. Missing preferred agent | **Existing local guard.** Panel clears rows/detail, emits null and Failed. Real page echo may transform the request through workspace AgentId; this whole-page sequence is not yet proven. | Preserve local fail-closed characterization, add real-page missing-ID retry/echo witness; never silently treat invalid request as All. |
| 9. Callback ownership | **Confirmed gap.** Notify reads mutable current fields; stale read catches can rethrow to outer unowned failure. Only access enum is deduplicated. | Callback effects tied to captured current target; no stale A callback/failure after B. |
| 10. Controlled real rendering | **Confirmed opportunity.** All real sections are in host markup. Timeline/metrics children are already service-free and shared with runtime details. | Preserve real children; surface has typed state/intents and no service registrations. |
| 11. Raw infrastructure errors | **Refute direct exception.Message rendering.** Current read exceptions escape; there is no bounded local error/retry view. Rendered persisted summaries/details are a different data contract. | Safe public lane error, no raw thrown exception; do not claim all persisted content was already sanitized. |
| 12. Extraction/sandbox/watch | **Not executed for Governance.** No panel CSS file exists. Existing UI/sandbox can host the future closure without Core. | G03 baseline before move, real child consumers and current assets; no inherited Overview timing claim. |

Actual points: panel LoadAsync begins near line 55, RefreshRunsAsync near 130, SelectRunAsync near 201; source inventory hashes anchor review. Details come from canonical Core reader; missing-run exception is currently untyped. Keep a bounded unavailable detail message without text-parsing that exception. Source evidence is sufficient to prepare the seam; exact semantic RED still determines the minimal edit.
