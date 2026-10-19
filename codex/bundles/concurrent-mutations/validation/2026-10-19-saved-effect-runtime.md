# Saved-effect runtime adapter

The [adapter design](../plan/P04-saved-effect-runtime-adapter.md) is implemented. Core
validates the bound original selection; MAF restores exactly one module function and
its original payload, applies current policy/authorization, releases publication guards
before journaling, and records the original effect. It creates no provider continuation
or replacement operation. The history wrapper forwards the explicit runtime port.

The final Release candidate passed all 70 discovered tests (18 Unit, 52 Integration),
including real file journals/publication exclusion, original-claim execution, current
denial, ambiguous providers, altered restoration, cancellation/uncertainty and a lost
commit acknowledgement. Normal invocation and host-failure coverage also passed.
The shared policy mapping has independent tests. No project reference was added.

Earlier candidates are retained honestly: the first passed 57/70; the first fixture
repair passed 66/70. Fixtures needed the admitted profile's organization catalog,
a custom Agent whose permissions were not restored by managed seed normalization,
the existing JSON result shape, and an invocation scope for denial replay. The second
repair corrected those fixtures; production was unchanged. The first repair's resource
record incorrectly says 67/70; its retained TRX-derived proof correctly records four
failures and is authoritative for that count.

Release builds, final portability enforcement, rendering, documentation and diff checks
passed. Six new static findings were reviewed (five actual runtime root-path uses and
one ordinal tool-name comparison); the baseline contains 14,090 reviewed rows and the
scan 15,610 protected findings. Architecture snapshot `snap-20261019034232-17d75aa6`
reported the same four type cycles, no new project references and no new non-advisory
compilation errors. Package advisories remain existing findings.

Frozen source/runtime verified unchanged. The final observation left zero observed
processes and zero database-name delta; eight unowned temporary paths were preserved.
Proof: [final candidate](../proof/2026-10-19-planning-authority/c4-saved-runtime-fixture-v2.json).
The two failed predecessor proofs remain alongside it.

Architecture gate passes for this adapter. Planning policy activation, the real task-plan
provider, schema-six owner, native/API/SPA actions, Process and Workflow propagation are
still required. Configured workspace tools remain unsupported by saved schema-five
dispatch because their original composition context is not retained.
