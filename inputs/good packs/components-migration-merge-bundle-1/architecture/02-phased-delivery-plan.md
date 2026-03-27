# Phased Delivery Plan

## Phase Order

| Phase | Subbundle | Goal | Blockers | Exit gate |
| --- | --- | --- | --- | --- |
| 1 | `01-foundation-and-governance` | create project skeleton, naming, ownership rules, request workflow | none | new project graph and governance accepted |
| 2 | `02-shared-wrapper-baselib-merge` | create `Common` and `BaseLib`, merge wrapper libraries safely | phase 1 | wrapper tests pass and no app yet depends on half-merged APIs |
| 3 | `03-canvaslib-extraction-and-hardening` | create `CanvasLib` from `ComponentKit` canvas source | phase 1 | canvas tests pass and preview-only pieces are excluded |
| 4 | `04-tailwind-and-asset-pipeline` | establish shared Tailwind ownership and local icon/assets strategy | phases 2-3 | generated shared CSS/assets come from CanDoItAll only |
| 5 | `05-sandbox-catalog` | create component catalog and tuning surface | phases 2-4 | grouped pages exist with screenshot proof and fake data scenarios |
| 6 | `06-mcp-documentation-server` | expose component docs/examples through MCP | phases 2-5 | MCP can answer usage questions from shared libraries |
| 7 | `07-candoitall-components-split-and-adoption` | move CanDoItAll-only shells/compositions and wire app to new libs | phases 2-5 | CanDoItAll builds and shared usage is explicit |
| 8 | `08-zyphonote-components-split-and-adoption` | move Zyphonote-only shells/compositions and wire app to new libs | phases 2-5 | Zyphonote builds and shared usage is explicit |
| 9 | `09-cross-app-validation-and-proof` | run final build, test, screenshot, UX, and ownership validation | phases 1-8 | QA + architect + manager sign-off |

## Critical Path

1. Define ownership and skeleton projects.
2. Merge wrappers into `BaseLib`.
3. Extract `CanvasLib`.
4. Stabilize CSS/assets ownership.
5. Stand up the sandbox.
6. Use the sandbox output to shape MCP docs.
7. Rewire CanDoItAll.
8. Rewire Zyphonote.
9. Validate both apps.

## Safe Parallel Work

- `BaseLib` wrapper merge and `CanvasLib` extraction can proceed in parallel after phase 1 if write scopes are separated.
- sandbox page authoring can begin once a first useful subset of `BaseLib` or `CanvasLib` is stable.
- MCP tool scaffolding can begin before all examples are complete, but documentation indexing should wait for sandbox group structure.
- CanDoItAll and Zyphonote app adoption should not happen in parallel until shared library APIs are frozen for that wave.

## Freeze Points

- Freeze shared public APIs before app adoption phases begin.
- Freeze shared Tailwind/icon asset layout before removing legacy includes from apps.
- Freeze sandbox group taxonomy before MCP indexing and screenshot baselines are created.

## Preferred Release Rhythm

- small integration wave
- test and screenshot proof
- lock acceptance
- only then start the next wave

Big-bang replacement across both apps is explicitly rejected.
