# Architecture exit review

PASS for the bounded in-place mutation/effect boundary. No project movement or new dependency edge was introduced.

The service-free controlled surface still owns raw filters, tags, tree expansion and access-rule draft. It receives immutable snapshots and typed intents; its no-service rendering/intent tests passed. The host retains UI orchestration, authoritative session reads, selection/access callbacks, effect admission, notifications and individual dialog references. It does not become another requested-selection store.

## Responsibilities and rejected alternatives

| Owner | Responsibility and lifetime | Rejected simpler alternative / direct proof |
|---|---|---|
| Immutable assignment attempt | Exact before/desired sets, expected revision, independent complete editor copy | Live editor or deferred toggle failed authoritative-before and retry semantics; Unit copy/independence and existing public-contract completeness guard |
| Scoped per-agent operations | Stable attempt admission, outcome retention, canonical classification, explicit reconciliation/adoption | Host generation alone failed A-B-A and reconstruction; Components plus Unit single-flight/old-ack tests |
| Focused commands | Existing workspace save classification and typed diagnostic facade | Broad editor commands would import unrelated normalization/delete/storage; real registered projection and file-store tests |
| Core proof publication | Capture diagnostic inputs, observe, revalidate, publish under catalog callback fence, retain read-only receipt | A larger catalog partial could not isolate diagnostic/publication races; real store detachment/input/index and SQL runtime-revision tests |
| Host preview attempt | Latest request token and result/busy ownership | Selected generation alone cannot prevent an old preview finally from clearing the latest preview; direct delayed test |
| Panel overlays | Global capability dialog reference and child I/O ownership | Selected agent is not the global catalog editor owner; real DialogHost, current-selection completion and non-navigation browser removal |
| Scoped fixed-agent launcher | Observe one non-idempotent dispatch, retain known chat or unresolved state | Canceling on target/panel change can hide a created chat; exact identity, delayed launch, reconstruction and real browser launcher |

The code uses immutable requests, typed outcomes and small semantic coordinators. It does not introduce an interface per method, controller hierarchy, universal service bag, event bus, outbox, reflection into private component fields, uninitialized production objects or count-based architecture tests. No new partial files were used as fake separation. Existing dialog partials were changed only for their lifetime.

## Dependency and analyzer evidence

Entry module snapshot: `snap-20260906200039-9c6cc38d`. Exit Core/module snapshot: `snap-20260906215047-e662dc8b`; 2 scoped projects, 384 documents and 1176 types. The compact [analysis](../proof/inventory/architecture-analysis.json) records the actual tool result. Scope is explicit: it is not a whole-solution dependency claim. Direct project-file comparison and five direct builds prove no changed project references.

The analyzer reports existing namespace/type cycles in module hosting, reference-data-cache nested Entry and image-tool nested ToolBuilder. Those owners are unchanged. No new project cycle is introduced. Twenty informational DI diagnostics concern factory registrations, and five informational Mermaid diagnostics concern truncated exports; these limit automated resolution, not compiler correctness. New-owner member-count findings are informational; splitting a cohesive state/outcome contract to satisfy a count would reduce clarity. Existing large setup-service warning is outside this child.

Public proof receipts keep configuration fingerprints private. API responses expose only target/attempt identity, typed disposition, checked timestamp and automatic-replay safety. Error mapping does not disclose raw upstream details. Actual Core, Module, Web, UI and broad Components direct builds passed. No provider/catalog wrapper was redesigned and live sibling mode is unchanged.

## Consistency and product limits

- Provider persistence and the file catalog are independent authorities. Asynchronous canonical provider revision refresh followed by synchronous last-observed revision comparison inside the file update is optimistic fencing, not a distributed transaction. A provider change after that final observation is not covered by a shared lock.
- Exact full input hashing is deliberately conservative: even a change irrelevant to one proof rule may supersede the diagnostic. It never licenses stale publication.
- Desired-set recovery proves the present postcondition, not historical causation. Exact unchanged precondition permits one explicit retry of the same submission; intervening state requires adoption. Failed/wrong/older evidence stays blocked.
- Recovery retention lasts for the DI circuit. It is not durable across a new circuit, process restart or machine. No durable recovery protocol was requested.
- An unknown Curator launch cannot prove a chat absent; the UI directs inspection of managed chats and prevents blind replay within the circuit. A known returned chat remains registered even if the panel disappeared.
- Global capability catalog create/edit persistence outcomes were not redesigned. This child hardens their overlay/read/diagnostic lifetime and observes dispatched saves. The historical details-load fallback and global capability first-create recovery remain outside this bounded assignment/proof work; do not advertise those dialogs as fully mutation-hardened.

No whole stable suite was invalidated: no common interface signature, storage schema, provider behavior, project graph, routing or base component changed. The existing workspace Task facade stays source compatible; changed verification behavior is covered by every owning runtime tool/API/editor path in the focused gates. API failure changes have direct compatibility tests. Provider/catalog predecessor proof remains historical.
