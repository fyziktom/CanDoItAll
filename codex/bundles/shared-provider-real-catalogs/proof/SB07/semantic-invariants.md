# SB07 semantic invariant contract

Owner: N011/R11 and N012/R12 in inputs/06-thinking-effort-feedback.md.
Source/test hashes: changed-files.csv (HEAD blob and captured pre-edit provenance are
separate). Production ownership/assertions: architecture-review.md and closure-audit.txt.

## SB07-I1: model-specific independent thinking

Source policy/discovery publishes typed support, control, levels, default and temperature
policy. The client consumes that snapshot by routing ID. Valid explicit effort wins;
omission resolves the current source default at dispatch. Unknown/unsupported or invalid
explicit values fail before upstream execution. Capability changes invalidate revision
and prepared-agent state. A label-only fix, guessed model-family support, provider-global
override or silently ignored setting is forbidden.

Failing-first: red.trx. Final positive/adversarial proof: unit-verification.trx,
components-verification.trx and relay-integration-after-discovery.trx. These cover absent
metadata, invalid levels, serialization, immutability, stale state and preserved overrides.
Downstream: SB08's same-model Low/High and changed-source-default real requests.

## SB07-I2: concise real model suggestions

The main OpenAI allowlist intersects actual inventory. Source publishes suggestion
membership; agent and Simple Chat dropdowns use natural real-name ordering. Saved older
models remain valid. Dropping real catalog/pricing membership, introducing made-up models
or sorting opaque routing IDs is forbidden. Regression proof: ProviderModelSelectorTests,
AgentProviderPresentationMapperTests and Simple Chat cases in the final focused TRX files.
The broad presentation assertion exposed the old-list expectation and was repaired;
it is not represented as a pre-edit failing-first test for this invariant.
Downstream: SB08 source-client-parity.json checks exact model labels and their order.

## SB07-I3: actual upstream execution and terminal usage

The source-owned temperature omission must survive opaque client routing. Supported
Responses SDK envelopes must pass bounded validation; completed Responses streams must
finish on a valid response.completed event, without requiring Chat's [DONE]. Failed,
incomplete, inconsistent terminal events and premature EOF must fail explicitly.
Failing-first: temperature-red.trx and responses-terminal-red.trx. Positive and negative
proof: unit-verification.trx and relay-integration-after-discovery.trx; sdk-envelope.trx
also retains the exact SDK-shape test. A synthetic success/usage row or permissive parser
is forbidden. Downstream: all nine SB08 source records are Succeeded/Complete.

| Production artifact | Producer | Consumer/lifecycle | Negative proof |
| --- | --- | --- | --- |
| Thinking catalog metadata | SharedProviderThinkingCapabilityMapper and CatalogProjection | Canonical revision, synchronization, ProfileMapper, prepared-agent fingerprint | Unit protocol/unknown/immutability cases |
| Request effort | Per-agent configuration and MAF request options | Current-source relay policy, real provider dispatch | Unsupported/invalid rejection and independent requests |
| Suggested membership | OpenAiModelSuggestions intersected with discovery | Publication/materialization, agent and Simple Chat selectors | Legacy selection and unsuggested-model regressions |
| Terminal usage | SharedProviderSseRelayStream | Relay completion and existing invocation ledger | Four Responses terminal regression cases and malformed/EOF cases |

Diagnostics must contain no credentials, prompts or reasoning traces. Bounded fixture
test inputs are not real upstream evidence. The closure audit names these invariant IDs.
