# Duplication and shared-surface risk

## Shared-surface warning

`CanvasWorkbench` and `CanvasFloatingWindow` are shared platform components, not ProjectStructure-only components.

Confirmed consumers include:
- ProjectStructure,
- PromptFactory,
- Sandbox.

Therefore every low-level change to:
- event routing,
- floating-window behavior,
- workbench state publish rules,
- renderer update paths,
- exported image behavior,

must be treated as a shared-platform change.

## PromptFactory risk

PromptFactory already depends on:
- the shared workbench shell,
- shared floating-window behavior,
- toolbox behavior,
- context and quick-create flows,
- browser automation against `__canvasWorkbenchState`.

If the shared canvas code changes without PromptFactory regression coverage, you can easily “fix” ProjectStructure while breaking PromptFactory.

## Sandbox risk

The Sandbox page is a lighter consumer, but it is still useful as a smoke surface for:
- help overlay,
- settings overlay,
- general workbench shell integrity.

## Duplicate canvas trees

There are two diverged trees:
- `src/CanDoItAll.ComponentKit`
- `src/CanDoItAll.Components.CanvasLib`

The runtime paths in this audit point to `CanDoItAll.Components.CanvasLib` as the active shared implementation.  
However, the duplicate tree is still a maintenance risk because:
- engineers can patch the wrong tree,
- later cherry-picks become confusing,
- duplicated fixes are error-prone.

## Recommendation

Do not try to consolidate duplicate trees at the beginning of this program.

First:
- stabilize ProjectStructure and shared-canvas behavior,
- add browser gates,
- finish the retained renderer path,
- then inventory which tree is actually canonical and remove or isolate the other.

## Practical safety rule

Any task that changes shared canvas code must run:
- relevant component tests,
- relevant ProjectStructure browser tests,
- relevant PromptFactory browser tests.
