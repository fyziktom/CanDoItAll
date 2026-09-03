# C# Testability Plan

## Observable contracts

| Seam | Production caller | Test substitution | Required assertions |
|---|---|---|---|
| SDK dependency baseline | Existing MAF, workflow, MCP and A2A adapters | Actual MAF 1.20/MEAI 10.9 assemblies; fake external endpoints only | One coherent resolved graph; representative schema/result/workflow/cancellation behavior is frozen before application repair. |
| Schema binding and feedback | MAF tool middleware | Real AIFunction with a counting delegate and captured model messages | Malformed input invokes zero times; safe field diagnostics; corrected nested request invokes once. |
| Result normalization | Invocation middleware | Real typed domain/workspace/MCP objects, SDK error text, unknown object | Failure/unknown cannot prove mutation success; successful supported reads remain valid. |
| Terminal assessment | Both interactive completion paths | Trusted typed trace sequence | Same-operation recovery, unrelated success, approval wait, cancel and no-tool answers remain distinct. |
| Durable safe outcome | Existing run/receipt writer and Web projection | Real persistence + HTTP host; fake external provider only | Restart/read round trip, legacy Unknown, redaction, run/receipt consistency. |
| Cross-turn projection | New runtime session creation | Two real canonical turns with isolated scope fixtures | Failure survives the next turn; no raw SDK/approval reuse; hostile/cross-scope data excluded. |
| Provider parity | Real SDK adapters and shared source endpoint | Scripted HTTP upstream at external boundary | Nested schema and tool IDs/results preserved, streaming and explicit rejection semantics equivalent. |
| Asset commit | Existing service + focused adapter | Real temporary managed storage, throwing analytics observer | Canonical identity/content exist after commit; error effect remains Committed; no second mutation. |
| Refresh | Existing context hub and page | bUnit lifecycle + canonical reader | Matching committed effect reloads once even if run fails; unrelated/disposed context does not. |
| Whole path | Web dispatch, runtime, storage and UI | Deterministic provider server, then actual Ollama | No direct service/API mutation bypass, truth of receipt/status/readback and visible graph. |

## Proof strength

SB01 and SB03 are Governed because diagnostics and cross-turn context are trust boundaries. Require failing-first and passing transcripts, source/test hashes, an invariant manifest, semantic proof and adversarial cases at execution closure. SB00/SB02/SB04/SB05/SB06 use Behavioral proof with explicit positive/negative cases. No manifests claim that unimplemented tests passed.

The planned exact cases and command recipe are in [validation-plan.md](../plan/validation-plan.md). New tests must exercise the production caller, not a disconnected helper or a test-created success receipt. Existing authorization and safe-diagnostic tests remain regression checks.

## Anti-stub audit

Reject string-presence tests as proof of a mutation, a fake tool delegate as end-to-end proof, direct node creation followed by refresh as agent proof, and screenshots without canonical readback. Do not expose a public injection property only for tests. Use existing DI boundaries, internal visibility or a narrow delegate only where it represents the real boundary.

## Production behavior artifact matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
|---|---|---|---|---|
| Typed invocation outcome and effect evidence | Trusted middleware/domain operation | Run assessment, safe receipt mapping | Create per call, persist, read with legacy Unknown | Unknown/model-authored envelope cannot claim commit. |
| Scoped prior tool-evidence projection | Canonical authorized run/receipt reader | Current turn's runtime input | Recompute for every turn using current access | Cross-project/agent/denied evidence excluded. |
| Scoped committed-effect notification | Run orchestration from trusted effects | Existing context provider/canvas reload | Publish, dedupe, dispose subscription | Later failure still refreshes; unrelated context does not. |
