# Assumptions And Risks

## Working Assumptions

- The user wants direct implementation in this turn, not only a plan.
- The current repository truth is preferred over stale documentation wording.
- The removed Processes and ProjectStructure MCP servers may return later, but current documentation should describe them as suppressed/retired and route users to HTTP APIs now.
- The external `CanDoItAll.Economy` repo is relevant for positioning and roadmap narrative, but this repo should not document internal classes or routes that are not present here.

## Critical Path Risks

- If the API-first replacement docs are weak, users may keep trying to reinstall removed MCPs.
- If the architecture diagram remains on `architecture-beta`, GitHub readers will see a parser error instead of the architecture overview.
- If generated image files are referenced before they are moved into `docs/images`, the customer docs will contain broken image links.

## Validation Risks

- The repo does not currently include Mermaid CLI; render validation may need to be limited to syntax-risk checks unless an available tool exists.
- `git diff --check` can catch whitespace but not semantic documentation drift.
- Image generation can produce imperfect in-image text, so Markdown captions and alt text must be treated as the authoritative documentation.

## Reopen Triggers

- Reopen subbundle 01 if any active docs still instruct users to install or call `candoitall_processes` or `candoitall_projectstructure`.
- Reopen subbundle 01 if `docs/architecture-beta.md` still contains an `architecture-beta` code fence.
- Reopen subbundle 02 if any of the four required audience infographics is missing from `docs/images` or not referenced by customer-facing docs.
- Reopen subbundle 03 if validation finds pending bundle rows, broken local image references, or Mermaid blocks with known risky syntax.
