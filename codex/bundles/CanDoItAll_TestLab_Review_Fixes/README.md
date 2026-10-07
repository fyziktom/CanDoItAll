# TestLab UI — reviewed closure repairs

**Entry point:** [prompt.md](prompt.md). Target: Codex GPT-6 Astra Max on the user's current checkout.

The TestLab extraction and the original S0 shell repair are substantially implemented. This is a **bounded corrective handoff**, not another extraction or permission to start the next module. Preserve the current contracts, renderer, production owner/session and standalone sandbox.

The review found two concrete sandbox parity defects: an interrupted committed read-back can strand the active draft in Pending, and a valid saved responsible party disappears from the available choices when its plan becomes global. A small notification wording correction also distinguishes an unknown production write outcome from a confirmed failed write.

Read [the source review](REVIEW.md), [the acceptance matrix](VALIDATION_MATRIX.md), [the evidence limits](PROOF_STATUS.md), [the source register](SOURCES.md), and the [original shared foundation](original/shared/prompt.md). The [original TestLab handoff](original/prompt.md) is retained in full as historical intent, including its [validation matrix](original/VALIDATION_MATRIX.md). Do not rerun its initial extraction instructions.

| File | Purpose |
| --- | --- |
| `prompt.md` | The current executable assignment and stopping boundary |
| `REVIEW.md` | What was inspected, what is accepted, reproducible source paths and priorities |
| `VALIDATION_MATRIX.md` | Required regression behavior, impact-based execution and closure |
| `PROOF_STATUS.md` | Source review versus author-reported execution and this package's checks |
| `NEXT_MODULE_NOTES.md` | Why the current run stays on TestLab; limited candidate inventory |
| `regression-seeds/` | Two proposed C# regression seeds; not compiled or run by the reviewer |
| `SOURCES.md`, `sources.json` | Pinned review sources, GitHub-reported blob identities and read coverage |
| `original/` | Complete previous TestLab package, preserved byte-for-byte |

Review provenance: `fyziktom/CanDoItAll`, `components-decoupling`, `3c579fd1a923ad90f619fe144e6e4c1fe081fa8b`. **This is not an execution pin.** Record and inspect the actual starting revision. Do not reset, rebase or checkout an old review commit.

All new engineering material is English. The owner-facing Czech review is delivered separately. The inherited shared foundation includes its original Czech README; preservation is intentional, not permission to introduce Czech into application code or documentation.

The reviewer did not build or run the product, browser tests or benchmarks. The two defects are source-supported execution paths; demonstrate them with failing-first tests on the actual checkout before changing code.
