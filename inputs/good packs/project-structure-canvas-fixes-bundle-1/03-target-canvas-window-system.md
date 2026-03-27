# Target Canvas Window System

This file defines the target interaction model for structure canvas windows.

## Visual Thesis

The structure page should behave like one full-width operational workspace where the canvas is the primary surface and every secondary tool feels like a movable workbench window, not a permanent page column.

## Core Window Types

Bundle 1 must support these in-canvas windows:

- `Selection panel`
- `Canvas health`

Bundle 1 should also review these utility surfaces for future migration or compaction:

- `Outline`
- `Graph health`

## Required Window Behaviors

Every in-canvas window in bundle 1 must support:

- drag and move
- resize
- minimize
- normalize to its default size and position
- hide
- show again from a clear toolbar or stage affordance
- focus elevation when interacted with

Window behavior rules:

- default spawn must never cover the toolbar
- toolbar must remain reachable at all times
- the active window may overlap canvas content, but not the toolbar safe zone
- minimized windows should collapse to compact chips or small capsules inside the canvas, not disappear with no recovery path
- normalized state should restore the page-defined default layout, not an arbitrary previous drag position

## Toolbar Contract

The toolbar must become the true top frame of the canvas.

Requirements:

- full-width inside the canvas stage
- pinned to the top in desktop and maximized modes
- compact and responsive before horizontal scroll is allowed
- less-used actions may move into an overflow menu when width is insufficient
- labels may collapse to icon-plus-tooltip where meaning stays clear
- the zoom rail should shrink progressively instead of forcing the whole toolbar into overflow too early

Implementation direction:

- keep one primary action group and one utility group
- add width-aware compaction rules
- reserve a top safe zone so floating panels start below the toolbar

## State Persistence

Window state should persist per project structure canvas view.

Persist:

- visibility
- minimized state
- normalized or custom position
- width and height
- z-order preference only if needed

Preferred persistence location:

- structure page workbench UI state alongside existing `ViewStateJson`

Reason:

- the user is arranging a workspace, not just opening a modal
- refresh and watch reload should not reset the working layout

## Recommended Component Shape

Shared component kit additions:

- a generic floating canvas window shell
- a shared JS interop module for drag, clamp, normalize, and resize observation
- a typed state object for panel geometry and visibility

Structure page additions:

- a selection panel host
- a validation health window host
- toolbar toggles for hidden windows

## Default Structure Page Window Layout

Recommended default desktop layout:

- toolbar pinned across the top of the stage
- health window near the upper-left, but below the toolbar safe zone
- selection panel near the upper-right, but inside the canvas
- both windows small enough to keep most of the canvas visible

Recommended default mobile behavior:

- toolbar compacts first
- windows can still open as floating surfaces, but should snap to a more constrained width
- if screen width is too small for useful drag behavior, the same shell may use a near-full-width anchored panel mode while preserving minimize and restore

## Why This Must Be Shared

If the structure page gets a custom floating system separate from prompt factory:

- the same bugs will reappear twice
- window rules will drift
- future canvas pages will repeat the same extraction work

Bundle 1 should leave the repo with one shared floating window pattern for canvas pages.
