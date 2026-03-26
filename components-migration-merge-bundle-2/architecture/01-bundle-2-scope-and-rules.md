# Bundle 2 Scope And Rules

## Scope

Bundle 2 covers the reusable component debt that bundle 1 left behind:

- missing Zyphonote primitives that should now live in `CanDoItAll.Components.BaseLib`
- existing `BaseLib` primitives that need parity expansion to replace Zyphonote wrappers cleanly
- reorganization of `BaseLib` into stable component-family subfolders
- shrinkage of `Zyphonote.Components` into a truly app-specific library with explicit ownership

Bundle 2 does not try to create a new shared music library, a new marketplace library, or a new planning library.

## Decision Rules

### Promote To `BaseLib`

Promote when all of the following are true:

- the component behavior is app-agnostic after neutral naming
- the component mostly wraps structure, slots, or layout rather than domain models
- styling can be expressed as shared Tailwind utilities or small family-scoped CSS
- the component is likely useful outside a single Zyphonote page

### Merge Into Existing `BaseLib`

Prefer merging instead of copying when the Zyphonote wrapper is only:

- a one-class container
- a renamed variant of an existing shared primitive
- a typography alias that `TextBlock` can already represent
- a card or form wrapper that only differs by a tone or slot layout

### Keep Local

Keep local when the component directly depends on:

- music theory, notation, MIDI, practice, or score-domain types
- marketplace or learning workflow DTOs
- Zyphonote-specific JS interop
- app-specific services or workflow state machines

### Move Feature-Local

A component can be real but still not belong in `Zyphonote.Components`.

Move it feature-local when it is:

- a score workbench shell or one-class workbench layout wrapper
- a page-specific list or picker that is not reused meaningfully across domains
- a workflow-specific modal that exists only to arrange already-shared primitives

## Architecture Corrections Bundle 2 Enforces

### `Zyphonote.Components` Must Stop Using Wildcard Ownership

Current state:

- `Zyphonote.Components.csproj` links `..\App.Blazor\Components\**\*.razor`
- a small removal list tries to carve out exceptions

This is backwards. The project currently exposes nearly everything by default and only removes what someone remembered to exclude.

Target state:

- `Zyphonote.Components` physically owns its own source files
- the project includes only those files explicitly
- page-local or workflow-local components remain in `App.Blazor`

### `BaseLibPrimitives.cs` Must Be Split

Current state:

- unrelated enums and services are stored in one file
- some values encode legacy names such as `SheetCardGhost`
- some shared APIs still use strings where typed enums should exist

Target state:

- family-local enums live next to their components
- services keep their own files
- legacy Zyphonote naming is not expanded in new shared APIs
- compatibility values may exist temporarily, but only as migration shims with an expiration path

### Namespace Stability Matters More Than Folder Flatness

`BaseLib` should gain folders without breaking consumer namespaces:

- keep `@namespace CanDoItAll.Components.BaseLib` in Razor components
- keep supporting C# types under the same root namespace unless there is a strong reason otherwise
- use folders for ownership and navigation, not for namespace churn
