# QA Prompt

Validate the executed subbundle against its proof contract, then update the execution report.

## Browser Review Questions

- Can all text be read without zooming?
- Are dialog, tooltip, and notification overlays free of clipping and harmful lateral overflow?
- Do overlays layer correctly above page chrome and each other?
- Do modal size variants look materially different and intentional?
- Does closing a dialog complete the displayed returned-object result exactly once?
- Does mobile width preserve readable controls and avoid overlapping sticky/sidebar chrome?

## Required Browser Analytics

Record each browser pass in `reviews/01-execution-report.md` with:

- subbundle id
- route
- viewport
- Playwright MCP actions and assertions
- screenshot path
- pass/fail result

## Closure Rule

- Do not mark a UI subbundle complete when only closed trigger states were validated.
- Do not mark dialog work complete without proving returned object behavior.
- Do not hide missing Playwright MCP proof in residual risk.
