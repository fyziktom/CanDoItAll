# S0 — bounded carry-over before Projects

## The exact open path

Start from the R2 case `SharedProviderTwoInstanceUiAcceptanceTests.Provider_empty_client_imports_shared_providers_and_runs_chat_image_and_vision`, not its separate existing-catalog convenience case. The failed point is the second Simple Chat editor for `UI Shared Ollama`: expected three model options, observed one within the current assertion deadline. The source catalog was complete. The cause remains unproven. [R01, R06, R07]

Use the existing owned publisher/client setup with scripted external responses, normal import/mirroring, real component events, real authentication and the existing explicit fixture-write opt-in. Do not use the ordinary app, retained provider installations or paid inference. Do not replace the import/metadata owner with a fake that simply hands the renderer three options and present that as the original path.

## Inspect and reproduce

Capture a compact safe timeline for each editor activation: renderer generation/source identity, selected provider ID, model IDs/options, provider-read start/completion, metadata revision, actual event dispatch/acknowledgement, circuit and view retirement. Do not dump credentials or full provider configurations. Keep provider setup, first OpenAI editor and second Ollama editor in their original order. Include a cold editor and the post-import state, not only repeated opens of the already warm catalog.

Trace `ExerciseSimpleChatAsync` in `SharedProviderMetadataUiChecks`, `LlmChatDefinitionEditorSession` and the actual `LlmChatDefinitionEditorSurface`. The session can publish the acquired form before provider loading completes; the surface has local immutable edits and a provider-derived model list. Determine the actual broken ordering; do not declare it an event/hydration issue from source shape alone. [R07–R09]

Add deterministic held-response/event controls for: provider list after editor acquisition; first-to-second provider change; metadata arriving after local selection; A→B→A; same-source rerender; closed/replaced dialog; and independent editor activation. Require unchanged typed user edits and exact provider/model identity. If a callback/hydration/readiness defect is shown, correct the smallest owning component or harness boundary and repeat the original configured case.

Increasing timeouts, fixed sleeps, losing local edits by resetting the form, ignoring the option assertion, automatically replacing the selected model, and marking any existing-catalog pass as equivalent are prohibited. A test-only synchronization repair must wait for a real acknowledged state, not manufacture it.

## Bounded exit

Preferred result: failing-first or causally controlled proof, bounded repair, original configured case plus negative controls passing on the recorded source pair.

If the original anomaly cannot be reproduced, do not invent a cause or claim it repaired. Preserve it as `REVALIDATED_WITHOUT_ROOT_CAUSE` only with the same configured cold/ordered acceptance path passing and the controlled late-event/provider cases passing. Document exactly why that residual, non-mutating selector uncertainty is isolated from Projects P1. This can support a qualified technical-entry decision; it is not global closure of historical evidence.

If the original configured case still fails without an explanation, or the investigation reveals wrong-provider authority, lost durable data or broad runtime changes, do not start mutating Projects implementation. Produce the causal mapping and blocked-entry result. Do not spend the run repeatedly observing warm selectors without adding discriminating evidence.

Update current publication/closure notes for Components development availability; keep historic entries immutable. Run narrow closure controls affected by the actual source delta. No full Stable/browser/viewport sweep merely for S0. Once the bounded entry is supported, continue into P1 in this same run.
