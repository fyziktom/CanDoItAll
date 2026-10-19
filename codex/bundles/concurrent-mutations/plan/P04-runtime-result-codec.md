# P04 runtime result codec boundary

Prepared before source edits on 2026-10-19. This is a prerequisite within the ongoing
ordinary Agent planning runtime implementation, not policy activation or E2E closure.

The scoped CodeAnalytics snapshot `snap-20261019030753-d550e953` loaded five projects,
516 documents, 1,901 types and 13,951 members. Its 189 diagnostics are NuGet advisory
messages; no other error was observed. The four reported cycles are type-level, not
project references. Actual project files confirm Models remains SDK-free, Runtime
Abstractions references Models, and Core/MAF consume these contracts in that direction.

`MafToolRunContext` currently owns SDK continuation, provider admission, invocation,
approval and result serialization. Its result codec is needed by the separate saved
effect adapter, which must never restore an SDK session just to dispatch an approved
original effect. Copying the codec creates divergent recovery semantics; adding that
adapter to the existing context grows an already broad runtime owner.

Extract result capture/restoration and disclosure decoding into one internal MAF codec.
Keep its persisted carrier and enum internal to MAF, with fixed numeric values. The
general protocol codec currently embeds `typeof(T).FullName`; therefore use one closed
typed mapping from the extracted carrier to the exact existing wire shape
`CanDoItAll.AgentFramework.Maf.MafToolRunContext+ResultCheckpoint`. All other types keep
their existing shape and package-fingerprint checks. Do not add arbitrary caller-supplied
shape aliases, read upgrades or alternate decoding fallback.

The existing invocation owner delegates serialization; provider origin, approval,
authorization, claims and effect accounting stay with their current owners. No project
reference, public API, general serializer or service-location seam is added. The new
codec can be tested without an SDK agent or filesystem host; the old context must shrink.

Validate literal historical envelopes and newly written shape/version/enum values,
committed versus pre-dispatch contradictions, unknown shapes, disclosure projection,
typed failures and browser canonical evidence. Run existing normal invocation/replay,
disclosure and provider protocol tests, standard Release builds and final portability
enforcement. A later saved-effect adapter must use this same codec and the actual Core
saved lease, receipt preflight, exact restoration and current owner authorization; this
extraction does not itself enable unsupported effects-only runtime execution.
