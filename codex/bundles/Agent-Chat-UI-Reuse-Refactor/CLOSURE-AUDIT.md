# Closure audit

The executor must answer at final closure:

## Scope

- Did any Simple Chat production UI, API, SSE, route, filter, or context feature enter the diff?
- Did any Simple Chat backend file change?
- Did final status stop at manual Agent regression?

## Architecture

- Does the neutral owner have a cohesive responsibility?
- Is it independently testable?
- Did old owners lose real responsibility?
- Are compatibility facades thin and intentional?
- Is the dependency graph acyclic and correctly directed?
- Are Agent/LlmChats/backend/persistence dependencies absent from neutral source?
- Did any named large UI type gain partial files?
- Did any boolean/source-switch god component or service locator appear?

## Behavior

- Are catalog, picker, threads, transcript, composer, approvals, execution, voice, attachments, prompt gallery, floating, settings, contextual windows, and Process consumers preserved?
- Are selectors, accessibility, focus, scrolling, overlay behavior, and large-desktop composition preserved?

## Tests

- Were actual diff line ranges used?
- Were workspaces healthy?
- Did every required selector run?
- Was discovery nonzero and expected?
- Were conditional selectors promoted when triggered?
- Was any broad gate justified and run at most once?

## Handoff

- Is the manual checklist complete?
- Is next-phase execution blocked on explicit user approval?
