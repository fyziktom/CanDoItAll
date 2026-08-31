# Current State

Prepared at commit `8a8dc2da0` (model-label fix committed); prior Docker CSS fix is `7fa05fe5a`. Working tree was clean before preparation.

## Evidence

Existing latest recorded Created→first Run stage: Docker5214 **30.158 s**, native5032 **18.216 s**. Historical medians: 25.696 s across nine client runs and 12.393 s across six native initial preparations. Different workloads/builds; no controlled comparison or guaranteed branch causality. Stage timestamps precede their own persisted commit; first Run is not exact provider network dispatch.

The progress callback awaits a normal chat transaction changing eight files, including journal/run/session/log/indexes. Repeated fresh policy creation probes filesystem case sensitivity even on reads. Scratch production-DLL 1 KiB writes at parent depth6 averaged 48.374 ms, with 20 policy creations accounting for 50.4%; this does not predict total startup speedup. Docker data is a Windows bind mount; changing it is outside this bundle.

The progress/durable filesystem path predates branch baseline `1625b336e`. Provider revision checking changed in `3045385c7` to full materialization. It occurs before run creation. Exact live attribution to locks/probes/validation/serialization is not retained, so Phase0 must measure it without weakening behavior.

## Current Safety Contracts

- Factory policy construction has fresh case/path semantics; singleton registration does not imply safe global caching.
- Shared revision loading performs validated availability, not only token lookup: malformed/tampered publication input yields null. Raw SQL can bypass tracked concurrency-token updates.
- Immediate/recovery existing-run commits currently share repeated persisted-state validation. Generic-new-run already distinguishes immediate commit from recovery.
- Every progress update advances activity and index metadata; unchanged usage observations do not imply unchanged usage/chat indexes.
- Floating list Stop is handle closure, not cancellation of a running request. Current running-state restrictions must remain.

## Architecture Evidence

Scoped CodeAnalytics snapshot `snap-20260831122755-8dc56aa3`: 5 source projects, 583 documents, 29 informational DI/EF interpretation diagnostics; no blocking snapshot error. No project cycle in the selected subgraph; two module and two type cycles already exist. This is not a whole-solution clean bill of health. Package/abstraction projects outside the selected snapshot were checked through project references.

Large persistence owners (2271-line store, 3372-line execution slice, 1542-line chat projection) justify a boundary gate, not a broad refactor. See compact JSON and architecture inventory.
