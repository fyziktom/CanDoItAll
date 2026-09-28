# Plugins review fixes → complete SchedulerPlanner UI decoupling

**Executable assignment for Codex GPT-6 Astra Max · 28 September 2026**

Start with [prompt.md](prompt.md). Execute **one combined assignment**: repair the two
bounded Plugins findings first, then implement the complete SchedulerPlanner rendering
boundary and standalone representative sandbox. Do not end after the Plugins fixes.

The review used `fyziktom/CanDoItAll`, `components-decoupling`, commit
`0e176a3b99270cdc9a86a57d6276d05979d7352e`. This is evidence provenance, **not a checkout
instruction**. Reconcile the actual user-provided branch, HEAD, worktree and changed consumers.

## Package map

| Document | Purpose |
| --- | --- |
| [Execution prompt](prompt.md) | Authorized work, sequence, architecture and closure |
| [Plugins review](PLUGINS_REVIEW.md) | Accepted implementation, two corrective findings and exact regression obligations |
| [Scheduler source review](SCHEDULER_REVIEW_NOTES.md) | Surface inventory, current risks, owner boundaries and consequence map |
| [Validation matrix](VALIDATION_MATRIX.md) | Behavioral, integration, asset, dependency and cleanup gates |
| [Development loop](DEV_LOOP.md) | Comparable Web/sandbox measurements, including real Canvas CSS and JavaScript |
| [Source register](SOURCES.md) | Pinned source locations, observed Git blobs and honest inspection coverage |
| [Proof status](PROOF_STATUS.md) | Reviewer inspection versus implementer-reported historical execution |
| [Package validation](PACKAGE_VALIDATION.md) | Results for this handoff's own tooling, hashes and links |
| [Shared foundation](shared/README.md) | Unmodified 22-file shared UI-decoupling v3 foundation |

This package includes guidance and package-check utilities, not a product patch, compiled
regression seeds, application binaries, fonts, screenshots or old local test logs. The
historical shared register is reference material; the current module register is
`sources.json`. Past module-specific bundles are not recursively included.

## Start command

```text
Read and execute CanDoItAll_SchedulerPlanner_UI_Decoupling/prompt.md.
Use the complete extracted package and the current repository instructions.
First reproduce and repair the bounded Plugins review findings.
Then finish the complete SchedulerPlanner UI extraction and its validation.
Do not stop after Plugins, and do not start a third module.
```

Package checks from this directory:

```text
python tools/validate_package.py
python tools/test_package.py
python shared/tools/validate_bundle.py
python shared/tools/test_tooling.py
```

Run package checks against the delivered UTF-8/LF archive. A Git checkout can normalize
line endings; inspect that difference on a disposable normalized copy rather than
rewriting sealed historical inputs or claiming a byte-for-byte match that did not occur.

Local signed implementation commits are permitted on the supplied branch. Remote push,
merge, release, branch reset, unrelated sibling edits and ordinary application/database
operations are not authorized. Keep the existing GPG session without disabling signing.
