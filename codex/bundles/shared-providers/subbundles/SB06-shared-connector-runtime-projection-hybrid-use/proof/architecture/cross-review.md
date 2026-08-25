# SB06 independent architecture and security cross-review

State: `PASS` after repair and re-review.

## Review chronology

The first final independent review rejected closure with three P1 findings:

1. runtime selected named clients did not propagate the active access-context reference;
2. source-managed profiles did not bind requested routing models to their selected publication;
3. shared health/runtime failures could disclose private endpoint, secret-reference, prompt, or raw
   transport details through returned diagnostics and activity.

The repairs added per-message request-scope context resolution, an exact publication model
constraint enforced by raw and MAF SDK paths, and a typed source-token failure disclosure policy at
driver/catalog/runtime/activity/workflow boundaries. Frozen 18/16/10 lanes and focused 52/13/4/10
driver, transport, workflow, and credential lanes passed after those changes.

A later architecture re-review found one more real P1: imported shared OpenAI chat profiles were
still eligible for speech-to-text/text-to-speech, and an explicitly configured ineligible shared
voice provider could silently resolve to the first personal provider.

The final repair:

- added connector-neutral typed source-managed audio denial at both OpenAI audio driver entry
  points before credential resolution or HTTP dispatch;
- filtered source-managed profiles from the existing voice picker;
- preserved an empty selection for an explicitly configured ineligible shared ID, allowing automatic
  personal selection only when no provider was previously configured;
- retained independently configured personal voice behavior.

Post-repair Unit and Integration solution builds pass with zero warnings/errors. Frozen lanes remain
18/18, 16/16, and 10/10. Audio-specific supporting lanes pass feature matrix 16/16, concrete drivers
54/54, and agent voice 29/29.

## Final architecture disposition

`PASS`; no remaining P1/P2 blocker.

- `provider.candoitall-shared` remains origin metadata around `ProviderKind.OpenAi`.
- No duplicate provider runtime or canonical store was introduced.
- Before/after selected references have zero delta.
- Final snapshot `snap-20260825100508-300644c7` has 14 projects, 34 direct references, zero project
  cycles, and zero errors; two module/one nested-type cycles remain unchanged.
- Inner MAF has no Workspace, SharedProviders Http, Web, or UI implementation edge.
- Audio, model, HTTP, credential, and disclosure policies are connector-neutral typed seams.
- The existing voice component edit is a narrow eligibility/no-fallback consumer guard, not an SB08
  provider-sharing UI implementation.

## Final security disposition

`PASS`; no remaining P1/P2 blocker.

- Source credentials use exact secret/source purpose and consumer scope.
- Context is per outbound message and does not leak through a cached client.
- Source-managed health/runtime messages exclude endpoint, secret, prompt, model, provider ID, raw
  response, and raw exception details.
- Source-managed STT/TTS denial happens before credential/network access and exposes safe public text.
- Explicit unavailable/model/audio selections fail closed without provider substitution.
- Caller-requested cancellation remains cancellation.

## Closure disposition

Implementation and review gates pass. Mechanical proof hashes, cumulative changed-file inventory,
closure validation, and root status/review/traceability updates remain the only packaging work before
SB07 can be unlocked. No broad, browser, Playwright, multi-instance, live-provider, or paid-provider
lane was used to reach this disposition.
