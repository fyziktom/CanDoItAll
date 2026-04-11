# QA Prompt

Validate the selected subbundle for `canvas-workbench-popover-hardening-2026-04-10`.

- Use real browser proof for any canvas hover or popover behavior.
- Start with a large desktop viewport.
- Open the popover state itself and verify readable content, no clipping, no harmful lateral overflow, and correct layering.
- Check that clicking the relevant node or annotation does not produce a console error and does not leave stale hover behavior behind.
- Record the exact route, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.
