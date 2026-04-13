# Inline Screenshot Reference

## Screenshot Summary

- The `/processes` workspace already fits inside the bounded shell, but the page still spends height on large summary cards, header spacing, and toolbar chrome.
- The process definition canvas is visibly cramped and several nodes overlap or crowd each other, making the map hard to read.
- The current toolbar exposes icon actions but no recomposition menu.
- The current canvas viewport leaves unused width when zoom is reduced slightly, which makes the working area feel narrower than the container.

## Visual Questions The Bundle Must Close

- Does the canvas use the width that is already available inside the definition panel when the zoom is slightly reduced?
- Do badge-style summary tiles keep the key metrics on a single line without hurting scanability?
- Do the recomposition commands produce visibly distinct outcomes instead of three nearly identical moves?
- Does the smarter process recomposition preserve a readable mainline-and-branches structure instead of merely exploding nodes outward?
