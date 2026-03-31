# Structured Input

## Core Objective

- Deliver a shared non-canvas BaseLib-centered theme system that uses semantic Tailwind-backed tokens, supports downstream CSS-variable override, and proves runtime light/dark switching.

## Hard Constraints

- Tailwind remains the source of truth for the shared style system.
- NuGet consumers must be able to override the shipped theme without rebuilding BaseLib’s Tailwind source.
- Public tone APIs stay descriptive and strongly typed. Do not introduce public shorthand strings such as `prim`, `sec`, or `dan`.
- The canonical shared non-canvas prefix must become `cad-*`.
- Runtime theme switching proof must happen on a real rendered route.

## Source Artifacts

- Raw user request
- Existing style-unification bundle for background context
- Generated workbook and CSV inventories for this bundle
- Repo source files listed in `inputs/01-source-artifacts.md`

## Input Coverage Signals

- Shared color system and theme basics
- Tailwind ownership
- BaseLib NuGet consumer override
- Best-practice semantic color naming
- Existing `cda-button--tone-primary` direction but inconsistent unification
- Prefix stabilization to `cad-*`
- Mandatory analysis lists and Excel artifacts
- Mandatory architecture subbundle and QA challenge
- Mandatory subbundle map for implementation
- Mandatory runtime dark-theme switching proof
- Mandatory Zyphonote compatibility confirmation without implementation

## Dependency And Sequencing Signals

- Architecture and QA challenge must finish before feature implementation.
- Theme-variable foundation must exist before BaseLib primitives migrate.
- BaseLib primitives must migrate before route-level hotspot cleanup.
- Runtime proof and Zyphonote compatibility confirmation belong at closure after the shipped contract exists.

## Validation Expectations

- Tailwind build must pass.
- Solution build must pass.
- Runtime theme switching must be shown on a rendered surface in the same session.
- Route screenshots must prove the shared contract on both demo and real app surfaces.
- Final closure must map each raw note to code and proof.

## UI Validation Strategy

- Start with a large-screen route pass at `1600x1000` or equivalent.
- Review screenshots for readability, hierarchy, spacing, alignment, and contrast after theme switching.
- Add a narrow/mobile follow-up pass for any layout-sensitive surface.
- Use the Sandbox foundations route plus at least one real app route as the runtime proof surface.

## Browser Validation Analytics

- Each UI subbundle will log route, viewport, proof mechanism, screenshot paths, and result in `reviews/01-execution-report.md`.
- If Playwright MCP is unavailable, record the blocker explicitly and use the best available CLI/browser proof instead of hiding the gap.

## Working Assumptions

- A scoped wrapper component is sufficient for theme switching because CSS variables cascade.
- CanvasLib remains a reference, not the primary migration target for this bundle.
- Existing descriptive enums should remain intact even if the CSS selector names change.

## Primary Risks

- A weak override contract would fail the NuGet-consumer requirement.
- A hard prefix cut could regress routes before proof catches it.
- Route-level cleanup could hide remaining primitive hard-coding if the foundation phases are weak.
