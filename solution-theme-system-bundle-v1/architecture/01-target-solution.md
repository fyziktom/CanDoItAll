# Target Solution

## Architectural Position

- The correct design is a semantic CSS-variable contract shipped by BaseLib, authored through the Tailwind workspace, and consumed by BaseLib primitives plus downstream routes.
- The wrong design is keeping palette values embedded in component selectors or replacing descriptive enum usage with short string class tokens such as `prim` or `sec`.

## Theme Contract

- Introduce a shared non-canvas token family with semantic names, not raw palette names.
- The contract should cover:
  - base surfaces and text
  - border and focus colors
  - control, card, dialog, and chip radii
  - action/status families for `primary`, `secondary`, `success`, `info`, `warning`, `danger`, and neutral/default variants
- The contract should be expressed as CSS custom properties scoped under a canonical host such as `[data-cad-theme="light"]` and `[data-cad-theme="dark"]`.

## Tailwind Integration

- Add the token contract to the Tailwind-owned CSS entry so the shared stylesheet remains the source of truth.
- Use `@theme inline` only where it improves Tailwind ergonomics for shared utilities.
- Keep the consumer override contract on plain CSS variables because NuGet consumers may not own the BaseLib Tailwind source tree.

## Runtime Host

- Add a small BaseLib wrapper surface that scopes the active theme by rendering a container with the current `data-cad-theme` value and an explicit `color-scheme`.
- Provide built-in constants for the shipped themes such as `light` and `dark`.
- Keep the API small. A wrapper plus constants is sufficient; a large state service is not required unless execution proves it is necessary.

## Prefix Model

- `cad-*` becomes the canonical shared non-canvas prefix.
- `cda-*` remains as a short-term compatibility alias where a direct rename is risky.
- `zy-*` remains tolerated only on explicitly excluded or still-unmigrated surfaces. New shared non-canvas work should not introduce more `zy-*`.

## Public API Discipline

- Keep descriptive enums for tone selection in C#.
- Do not expose `prim`, `sec`, `dan`, or similar string shortcuts as a public API. The token savings are not worth the maintenance cost.
- Existing enums such as `Light`, `Base`, and `Dark` may remain for compatibility, but the architecture should treat them as legacy-neutral variants, not the primary design vocabulary for new work.

## Expected Implementation Shape

- Tailwind foundation file adds semantic variables and built-in light/dark themes.
- Shared Tailwind component files move from palette utilities to semantic variables.
- BaseLib Razor components reduce direct palette strings and instead bind to the shared selectors and tokenized surfaces.
- Real module routes are cleaned up where they still bypass BaseLib or encode raw palette utilities.
- Runtime theme switching is demonstrated on a visible route without relying on a full page reload.

## Rejected Alternatives

- Rejected: public shorthand strings for tones
- Rejected: consumer override by editing BaseLib CSS directly
- Rejected: one-off dark-mode classes scattered through pages
- Rejected: prefix cleanup without compatibility selectors or scope boundaries
