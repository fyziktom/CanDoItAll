# CanDoItAll · Memory UI decoupling

**Assignment:** `CDA-MEMORY-UI-DECOUPLING-v1` · **Agent:** Codex GPT-6 Astra Max

Execute [prompt.md](prompt.md). First close the two bounded Scheduler findings in
[SCHEDULER_REVIEW.md](SCHEDULER_REVIEW.md), then complete the existing seven-tab
Memory workspace at `/memory`. Do not stop after Scheduler and do not extract a third module.

## Input chronology

The implementation under review is **`14f07bffa30ddb124011869e38bc5b264b8bce42`**.
The observed branch HEAD is **`b55baa3a94ff216353768f0a4ac9a50e89463d08`**.
Their comparison contains only the 36 historical Scheduler bundle files under
`codex/bundles/CanDoItAll_SchedulerPlanner_UI_Decoupling/`. The latter is not an
unfinished implementation or an instruction to execute the previous bundle again.
Use the actual current checkout and record drift; neither SHA is an execution pin. [EV01–EV03]

## Read and use

| Document | Purpose |
| --- | --- |
| [Prompt](prompt.md) | One executable assignment, sequencing and scope |
| [Scheduler review](SCHEDULER_REVIEW.md) | Accepted work, two source findings and failing-first scenarios |
| [Memory review](MEMORY_REVIEW_NOTES.md) | Existing owners, dependency cut, seven-tab coverage and safety constraints |
| [Validation matrix](VALIDATION_MATRIX.md) | Concrete obligations and proof boundaries |
| [Development loop](DEV_LOOP.md) | Before/after graph, assets and watch observations |
| [Sources](SOURCES.md) / [JSON register](sources.json) | Pinned files, metadata and precise review coverage |
| [Proof status](PROOF_STATUS.md) | Reviewer observations versus implementer claims and future tests |
| [Shared foundation](shared/README.md) | Unmodified shared v3; current repository guidance remains authoritative |

The complete `shared/` foundation is included once, byte-for-byte from the previous
handoff. Its historical module map and baseline commit are context, not current
completion status. Prior module bundles are not recursively copied.

**Capability boundary:** a complete Memory UI does not mean new provider features.
At the reviewed implementation, shipped drivers do not execute manual ingestion,
feedback, event acknowledgement or cancellation. Keep their current unavailable
behavior and prove server-side rejection; do not enable them to manufacture green
browser journeys. Preserve all seven tabs and the existing ledgers and diagnostics. [ME09, ME11]

## Local package checks

```text
python tools/validate_package.py
python tools/test_package.py
python shared/tools/validate_bundle.py
python shared/tools/test_tooling.py
```

These utilities validate the handoff only. They neither build the product nor prove
that the source findings reproduce. No application patch, binary, font, credential,
TRX or historical browser screenshot is included. See
[package validation](PACKAGE_VALIDATION.md) for the actual packaging checks.
