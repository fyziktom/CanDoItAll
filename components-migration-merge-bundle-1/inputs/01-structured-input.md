# Structured Input

## Core Objective

Prepare an implementation-grade migration bundle that moves the reusable component architecture under CanDoItAll without performing the migration itself.

## Hard Constraints

- Only this bundle folder may be changed during bundle preparation.
- CanDoItAll is the future authority for shared component libraries.
- CanDoItAll canvas is the source of truth for canvas contracts, JS, CSS, and components.
- Zyphonote is the better current source for many non-canvas Razor wrapper improvements.
- Not every component should become shared.
- The bundle must be phased and safe for implementation agents.
- The bundle must contain prompts, checklists, validation rules, tests coverage guidance, and proof expectations.
- Future shared-library edits must be owned from the CanDoItAll side only.
- UI validation must be screenshot-based and must include senior QA/UX/UI judgment.

## Source Repositories

- `C:\repositories\CanDoItAll`
- `C:\repositories\Zyphonote`

## Required Target Libraries

- `CanDoItAll.Components.Common`
- `CanDoItAll.Components.BaseLib`
- `CanDoItAll.Components.CanvasLib`
- `CanDoItAll.Mcp.Components`
- `CanDoItAll.Components.Sandbox`
- `CanDoItAll.Components`
- `Zyphonote.Components`

## Required Planning Topics

- architecture and dependency boundaries
- complete component classification
- CSS and Tailwind consolidation strategy
- sandbox catalog strategy
- MCP documentation server strategy
- staged adoption in both apps
- change-request governance for shared libs
- validation, proof, and sign-off rules

## Working Assumptions Made In This Bundle

- `CanDoItAll.Components.Common` stays small. It should hold light value types and helper primitives, not full UI services.
- `CanDoItAll.Components.BaseLib` owns the merged wrapper library and generic page-surface components.
- `CanDoItAll.Components.CanvasLib` owns canvas contracts, runtime components, JS interop, and canvas CSS.
- preview, tuning, and demo-only components do not belong in runtime libraries; they move into `CanDoItAll.Components.Sandbox`.
- the existing `CanDoItAll.Tests.Components` project is the right place to extend component test coverage first; there is no need to split tests into many new projects immediately.
- the existing Playwright suites in both repos should be extended instead of replaced.
- promotion from `Zyphonote\App.Blazor\Components` must be conservative. High reuse and low domain coupling are required for entry into `BaseLib`.
- `zyphonote-compat.css` is migration source material, not a shared library dependency.

## Primary Risks

- over-sharing branded or domain-specific Zyphonote components into `BaseLib`
- pulling Zyphonote global CSS debt into the shared layer
- moving CanDoItAll preview/demo components into production libraries
- breaking current canvas consumers in Zyphonote while extracting `CanvasLib`
- keeping external icon/CDN dependencies hidden inside shared components
- rewiring namespaces too early and causing avoidable big-bang churn
