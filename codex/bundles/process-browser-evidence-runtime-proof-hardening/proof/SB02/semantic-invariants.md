# SB02 Semantic Invariants

## Raw Notes Owned

- `N001`: "final app was not properly tested"
- `N003`: "items in tetris are not comming ... not visible"
- `N004`: "there is some js trouble in console output"
- `N005`: "this should not happen when I run complicated process like this"

## Shallow-Pass Trap

A browser proof that loads the route, toggles pause, and mentions a screenshot can still miss invisible gameplay or active JavaScript errors. Tests must reject that pattern when the step contract or project structure requires representative interaction.

## Adversarial Negative Proof Required

- Missing process-visible screenshot while markdown references a raw `.playwright-mcp` screenshot must fail.
- Active console errors during validation must fail.
- A generic interactive UI proof with only page title, body text, or pause state must fail when project structure requires representative visible behavior.

## Semantic Positive Proof Required

A UI/browser QA step with screenshot, console, DOM/evaluate, and representative interaction artifacts must reach quality acceptance only after the artifacts are durable and validated.

## Anti-Stub Audit Required

Audit for fixture-specific branching, product-name checks, status-only tests, and logic that treats all console disconnects as success or failure without phase classification.

## Production Behavior Artifact Matrix

| Artifact or signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Browser proof gate result | Process validation | Step outcome and recovery directive | Computed from process-visible artifacts before transition | Markdown-only proof cannot pass |
| Console phase classification | Console validator | Evidence pack validation | Active proof and cleanup boundaries evaluated separately | Active error cannot be masked by post-stop classification |
| Interaction adequacy finding | Runtime proof validator | QA step outcome | Derived from project structure or step contract and evidence artifacts | Page-load-only or pause-only proof cannot pass interactive requirement |

## Raw-Note Literal Closure

`N001`, `N003`, and `N004` remain open until tests prove the process catches shallow visual proof and active console defects.
