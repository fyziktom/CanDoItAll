# Real request invariant contract

UI-created dedicated agents on 5212, no changes to user agents:

- OpenAI gpt-5.4-mini low and high: same model, different effort.
- OpenAI gpt-5.6-luna low: different model, same effort.
- Ollama gptoss20b64k:latest low and high.
- Provider-default request and unsupported gpt-4.1 UI state.

Each successful case requires saved/reloaded UI settings, upstream HTTP success
with exact applied effort and request ID, assistant response and source usage.
No model self-report, mock upstream or inference from token counts counts as proof.
Source/5212/5214 data remains; all containers use the same build.

## SB08-I1: actual per-agent execution, N011/R11

Expected: distinct persisted UI settings reach real upstreams independently; omitted
effort follows the current source default without a client reload/sync. Explicit Low
must still win over a High source default. Disallow label-only acceptance, model
self-report, token-count inference, fake upstreams or manually seeded success rows.
Positive: mcp-results.json plus live-source-dispatch.txt and live-source-usage.txt, joined
by exact request IDs. Negative: unsupported GPT-4.1 UI, the actual rejected preliminary
Chat Completions requests, and SB07 invalid-override/terminal regressions. Earlier
compatibility-adjusted Chat Low-to-None is excluded from positive proof. Final default
is restored to Medium. Source hashes and red/green regressions belong to SB07.

## SB08-I2: faithful usable UI and preserved hosts, N012/R12

Expected: exact source/client real model labels and effort choices, natural ordering,
readable desktop dialogs, same final image and retained data volumes. Disallow count-only
parity, sorting hashes, resetting user data or a stale running binary. Proof:
source-client-parity.json, browser-validation.md, inspected PNGs and final-health.json.
The unsupported-model screenshot supplies the negative UI state. Deployment lineage:
docker-build-final6.txt, restart-final6.txt and restart-5214-final6.txt.

| Production artifact | Producer | Consumer/lifecycle | Negative proof |
| --- | --- | --- | --- |
| Agent override | Real client UI save/reopen | MAF, shared relay, actual OpenAI/Ollama, persisted response | Unsupported model and explicit-over-default cases |
| Invocation usage | Actual upstream completed stream | Source invocation ledger | Preliminary terminal failure and SB07 failed/incomplete stream tests |
| Mirrored catalog | Source real provider publication | Client UI synchronization and selectors | GPT-4.1 disabled; no invented models or opaque labels |

The downstream handoff is the three healthy deployed instances. closure-audit.txt in
SB07 names both invariant IDs; the final verifier rereads the actual proof artifacts.
