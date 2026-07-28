# Assumptions And Risks

## Assumptions

- The most useful first milestone is immediate truthful feedback plus elimination of duplicated storage work; fully preconstructing live runtime agents would be unsafe and is not required.
- Module UI projections are read-only current facts for prompt context, not canonical write models.
- Pre-run activities are operational observations. Durable execution logs remain canonical run history.
- A future SSE endpoint should project a filtered typed stream after authorization; it must not expose an unrestricted global bus.
- Timing assertions use ordering and operation counts in CI, with median/p95 artifacts for human comparison rather than brittle absolute wall-clock thresholds.

## Critical Path Risks

- SB02 is critical: weak identity, ordering, overflow, handler isolation, or lifecycle semantics would contaminate every module and future SSE work.
- SB03 is critical: pooling mutable runtime state can leak credentials, policies, approvals, tools, sessions, disposables, or context between runs.
- SB04 is critical: a stale module snapshot can answer from obsolete state or later be mistaken for canonical truth.
- SB05 is the hard UI gate: cosmetic progress without measured backend benefit does not satisfy the request.
- Changing persistence-before-publish semantics can alter exception behavior and recovery; durable writes and operational notification failures must have separate explicit outcomes.
- Concurrency may improve latency while reducing snapshot coherence or increasing file/DB contention.

## Validation Risks

- File I/O and PostgreSQL timing varies by machine; record cold/warm medians and p95 but assert deterministic ordering/read-write counts.
- One paid `gpt-5.4-mini` invocation proves wiring, not a distribution; fake-runtime/composition harnesses carry most performance proof.
- Provider dispatch-gate contention and credential cold start can dominate the one real sample.
- Process/project interactions require seeded data and may be difficult to reproduce in browser automation; capture exact fixture IDs and setup.
- Port 5032 may already be owned by another host; resolve the exact process before restart.

## Reopen Triggers

- Any cross-stream leakage, out-of-order terminal activity, undisposed subscriber, or swallowed canonical failure reopens SB02 and all downstream consumers.
- Any blueprint containing credential material, live handles, `DbContext`, mutable collections, transient authorization, approvals, or context-contributor output reopens SB03.
- Any snapshot accepted after a newer revision or written back to canonical storage reopens SB04.
- Any after measurement that does not improve time-to-first-activity immediately or materially improve/hold time-to-runtime-start blocks SB06 and reopens SB03/SB04.
- Any UI status derived from localized/magic strings or selected-run matching reopens SB06.
- Any SharedInfo contract divergence, Terra-backed test, failed build, or unhealthy 5032 host blocks closure.
