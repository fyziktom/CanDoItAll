# Implementation Prompt

Execute this bundle **sequentially**.

Non-negotiable execution rules:

- Preserve the product direction that node remains the universal carrier.
- Preserve semantic X/Y coordinates and marker meaning as canonical data.
- Do not ship new plugin work until SB01-SB05 are complete.
- Keep existing public structure DTOs and routes stable wherever feasible; prefer internal migration with adapters.
- Add tests as you go; do not postpone architecture guardrails to the final phase.
- After each subbundle, update the bundle evidence and rerun the review checks before continuing.

Required proof in the real environment:

- `dotnet build`
- targeted integration tests for changed seams
- component/playwright proof where UI contracts change
- refreshed canonical review artifacts and scorecard after SB05
