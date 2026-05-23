# Implementation Prompt

Implement only generic process runtime fixes for proof validation, browser evidence classification, and missing upstream artifact materialization. Do not add product-specific conditions for Tetris, Blazor, canvas, or games.

Preserve current behavior where the current step owns its own required output artifacts. Only configured upstream artifact inputs should route recovery to a previous producing step.
