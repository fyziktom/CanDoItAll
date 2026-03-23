# QA Prompt: Component Consistency Review

Review whether the implementation actually used shared composition patterns instead of creating new page-local variants.

## Focus Areas

- use of `ComponentKit` page-composition components
- reduction of bespoke list/detail wrappers
- reduction of bespoke sticky action areas
- consistent empty/loading state usage
- consistent badge/chip semantics

## Questions To Answer

1. Did the implementation create shared solutions or just nicer one-off page markup?
2. Are similar page sections now built from the same primitives?
3. Are there still obvious routes bypassing the new shared patterns?
4. Did the implementation accidentally place page-composition components in the wrong library?

## Required Output

- findings first
- identify duplicated patterns that should be consolidated
- identify components that were added but not broadly adopted

