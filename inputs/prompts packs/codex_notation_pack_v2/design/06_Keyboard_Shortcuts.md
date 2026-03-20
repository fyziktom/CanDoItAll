# 06 — Keyboard shortcuts (recommended defaults)

These defaults aim to match what users expect from common notation editors, while remaining easy to implement.

## Core tools
- `S`: Select tool
- `N`: Note tool
- `R`: Rest tool
- `E` or `Backspace`: Eraser tool
- `Esc`: back to Select tool

## Durations
- `1`: whole
- `2`: half
- `4`: quarter
- `8`: eighth
- `6` or `16`: sixteenth (choose one; many editors use `6` because `16` is awkward)
- `3` or `32`: 32nd (optional)
- `.`: toggle dot (cycle 0→1→2→0 if you support double dots)

## Accidentals
- `#`: sharp
- `b`: flat
- `n`: natural
- `Shift+#`: double sharp (optional)
- `Shift+b`: double flat (optional)

Behavior:
- If a note is selected: modify selected note(s).
- If in Note tool: set “sticky accidental” for the next inserted note.

## Navigation and editing
- Arrow keys: move selection left/right (time) and up/down (pitch)
- `Enter`: insert note at selection (step-time)
- `Delete`: delete selection
- `Ctrl/Cmd+Z`: undo
- `Ctrl/Cmd+Y` or `Ctrl/Cmd+Shift+Z`: redo
- `Ctrl/Cmd+C` / `V` / `X`: copy/paste/cut (planned)

## Slur / tie / articulation quick toggles
- `T`: toggle tie start/stop on selection (context-aware)
- `Shift+S`: slur tool (or `L` for legato)
- `A`: accent
- `-`: tenuto
- `0`: staccato

## Dynamics
- `P`: cycle pp/p/mp/mf/f/ff (optional)
- `<` / `>`: hairpin cresc/decresc (context tool)

## View
- `Space` (hold): show radial menu at pointer (recommended)
- `Ctrl/Cmd+0`: zoom reset
- `Ctrl/Cmd+Plus/Minus`: zoom in/out
