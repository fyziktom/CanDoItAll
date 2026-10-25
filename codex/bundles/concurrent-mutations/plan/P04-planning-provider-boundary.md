# Actual planning provider boundary

This source card follows the 114-case live owner checkpoint. The bounded next slice
connects a configured MAF provider/SDK session to the actual owner, without changing the
ordinary chat/recovery path. Product admission and publication remain later cutovers;
the provider slice must have a composition entry and real SDK/file-owner proof.

## Responsibilities and dependencies

Core owns a narrow planning-step runtime port and a module participant contract. The
participant owns the target's coherent basis read, current read authorization, response
schema and pure response-to-publication preparation. MAF cannot read Projects/Workbench
entities or parse task fields. The existing Core decision owner remains the sole live
interval authority. A runtime request carries the owner-issued handle and selected
Agent/provider, not a caller-provided elapsed duration or copied grant.

MAF implements the transport step using its existing configured provider factory.
`MafAgentRuntime` exposes this small port beside its existing ports. The actual
`MafToolAdmissionChatClient` boundary recognizes an explicit planning context, verifies
the final input and persists provider entry before forwarding it. Ordinary admission
and planning contexts cannot coexist. No new execution-service partial, project,
provider registry, store, lock, or general orchestration framework is justified.

The participant contract belongs in Core because it is an application/module boundary
and uses the actual live owner. Putting it in Runtime.Abstractions would introduce a
reverse assembly dependency on Core. Current Core -> Runtime.Abstractions/Models and
MAF -> Core directions remain unchanged. Workbench will implement the participant in
its existing module and register it through its composition boundary.

## Exact input and provider lifetime

Read the original journal under the active handle and require the original Agent,
provider identity/model, selected step and basis. Compose input from the immutable
original request, original target and selected basis, plus the participant's explicit
instructions and response schema. Do not include prior conversational facts, an old
provider conversation ID, SDK session state, context-provider output or tools.

Create a fresh configured MAF Agent/session per logical step, with
`UseProvidedChatClientAsIs = true`, no automatic function invocation, no background
response and framework-managed history. The first slice uses the non-streaming SDK
entry; accidental streaming under its context refuses before transport. The outer
admission client compares the actual SDK input/options with the prepared contract and
persists the exact final request digest. It calls current read authorization at that
boundary. Same-step inner transport retries retain identical input and one logical
dispatch; a new step gets new input and a new persisted dispatch.

Before transport, recheck active work, basis age and journal capacity. A linked remaining
work deadline bounds the provider call; provider cancellation/exception does not create
an automatic replacement dispatch. If the provider drains after cancellation or the
deadline, retain its actual bounded response via the outcome path. Unsupported function
calls or invalid structured output create no publication. A projection exception must
not discard an otherwise retainable response or silently fabricate a tool result.

Only the real provider response is saved in the journal, before returning control to
the caller/SDK. Unknown file acknowledgement leaves the original dispatch/response for
inspection; do not retry with a new identity. Provider disposal and failure diagnostics
must retain any already-saved outcome. Existing provider usage normalization is reused;
no fabricated zero-usage success or SDK continuation checkpoint is introduced.

A participant can explicitly accept an intermediate response with `Continue`. That
creates a new logical step only through the existing owner; it is not an invalid-output
retry. Invalid output is recorded as a typed refusal and cannot silently request another
provider call. Each step retains the original request and selects its basis according
to the captured policy.

## Bounds and publication follow-through

Reserve space before transport for the bounded response and its one prepared publication,
including the duplicated immutable source binding in the decision graph. Use explicit
UTF-8 and serializer-escape upper bounds, with metadata allowance; current decision and
journal sizes must leave that reservation. A capacity refusal occurs before the provider
call, not after a supposedly accepted proposal. Raw response and prepared payload bounds
are checked again before admission. Oversized/unsupported output keeps a failed original
dispatch; no automatic redispatch or effect is permitted.

The Workbench v2 preparation should bind the basis ID/digest already in the original
decision instead of copying the full material snapshot into multiple command envelopes.
Its restore/publication adapter must obtain that exact basis from the held original
run snapshot. It cannot substitute a current read. V1 encoding, wall-age interpretation,
receipt keys and decoding stay unchanged; dynamic work expenditure never enters the
original operation digest. This v2 change is not implied complete by provider tests.

For the first live provider slice, the complete input is limited to 2 MiB, encoded
response and prepared arguments to 256 KiB each, text output to 128 KiB, and output
tokens to 8,192. Source preparation
keeps its existing 64 KiB limit; it must reference the retained material basis. Tool and
call names are bounded to 512 characters. Reserve six UTF-8 bytes per input byte for
worst-case JSON string escaping, count the source envelope twice, and allow another
128 KiB for fixed record/identity metadata. This reserves 3.875 MiB in the aggregate
journal and 512 KiB in the decision before transport. These are safe upper bounds, not
a claim of measured maximum-size throughput. Enforce the same payload limits before
response admission. Ordinary journal/schema limits are unchanged.

Capacity measurement uses a counting stream, not another whole-journal byte array. It
runs under the existing atomic provider-entry callback to cover the exact version being
published; the bounded serialization cost must be included in later maximum-size and
contention qualification. Provider duration uses the original handle's remaining work,
not a new per-call budget. Known-response persistence has its own bounded completion
window after the network drains, so caller cancellation cannot silently discard output.

## Validation before dependent work

Use real SDK Agent/session construction with a deterministic provider transport and
actual file stores. Assert exact two-step input contents for both basis policies, no
old SDK history, distinct step/dispatch origins, absence of tool execution, request
tampering refusal, current-read denial before network, budget deadline, output bounds,
known late responses and lost acknowledgements. Include configured provider factory
composition and ordinary/native tool admission regression lanes. Rebuild in Release,
refresh CodeAnalytics, enforce portability-static, freeze inputs, then retain test and
resource evidence before editing the next candidate. Actual UI/approval publication,
Process/Workflow producers and sustained/max-size capacity remain separate open gates.
