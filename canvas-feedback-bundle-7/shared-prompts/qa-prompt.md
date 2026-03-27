# Shared QA Prompt

```text
QA this feedback7 implementation against the raw notes, not against assumptions.

Check:
- path-backed nodes no longer dump the whole path into the card body
- the compact path affordance exposes the full path and gives visible copied-state feedback
- file-backed paths promote the file name on the node
- non-preview double-click opens a centered quick-action modal with the expected labels and square-button treatment
- settings uses iconography instead of `cfg`
- the settings overlay stays below the toolbar on wide and narrower layouts

If anything is only partially proven, mark it as a gap instead of accepting it.
```
