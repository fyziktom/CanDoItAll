# Recommended Design Rules

## 1. One Page, One Primary Introduction

- do not show both a large shell route intro and a second large page intro
- the shell may provide compact context
- the page header should own the task framing

## 2. Standard Page Rhythm

- use one major page scaffold per route
- major vertical sections should use a consistent rhythm
- prefer a predictable pattern:
  - page header
  - optional summary/context band
  - main content shell
  - sticky actions only when needed

Recommended spacing rhythm:

- 24px between major page zones
- 16px between related cards/sections
- 12px between form rows inside one section

## 3. Page Header Standards

- title and subtitle belong at the top of the page content
- primary action belongs in the page header whenever the page has a clear create/run/save entry action
- if a page has no global primary action, omit the header action region instead of inventing filler

## 4. Action Hierarchy

- one clear primary action per page scope
- secondary actions should not visually compete with the primary action
- destructive actions must be visually separated from save/reset actions
- list-row actions should be compact and subordinate to row selection/opening

## 5. List + Detail Rules

- every list/detail page must show selected state clearly
- list pane needs:
  - title
  - count
  - optional search/filter
  - primary create action
- detail pane needs:
  - selected item title
  - status/meta context
  - structured sections

## 6. Form Layout Rules

- do not let large forms read as one uninterrupted vertical stack
- group fields into named sections
- keep related fields on shared rows only when they are naturally paired
- use a sticky action footer when the full form exceeds one viewport height

## 7. Inline Edit Vs Dialog

- use inline detail-pane editing for large entities with many fields
- reserve dialogs for:
  - confirmations
  - tiny single-purpose creates
  - focused pickers
- do not convert current long editors into modals in phase 1

## 8. Filters And Search Placement

- filter/search controls belong at the top of the list pane or result pane
- never hide the only search affordance deep inside a content card
- no-results state must be distinct from no-data state

## 9. Sticky Actions

- use sticky save/cancel regions on long editors
- do not make short forms sticky by default
- sticky action bars must preserve enough bottom spacing that content is not obscured

## 10. Card Vs Table Vs Split Panel

- use cards for mixed-density content and metadata-heavy items
- use tables only when comparison across rows is the primary job
- use split panels for selection-plus-edit or surface-plus-detail workflows

Phase-1 implication:

- most current management pages are split-panel candidates, not table candidates

## 11. Max Width Rules

- standard management pages should feel contained
- focus workbench routes should be allowed wider layouts

Recommended guidance:

- standard pages: comfortable max width, not edge-to-edge
- focus workbench pages: near-full width after shell chrome is reduced

## 12. Empty / Loading / Error States

- never use only a bare sentence where a reusable state component is warranted
- empty state must explain the situation and suggest the next action
- loading state should look intentional and preserve layout expectations
- error state should tell the user what failed and what to try next

## 13. Status And Badge Semantics

- use consistent status tones:
  - success: valid, enabled, completed, succeeded
  - warning: needs attention, blocked, incomplete, pending review
  - info: active, running, contextual metadata
  - neutral: background metadata
- avoid inventing custom inline pill styles per page

## 14. Visual Density Guidance

- keep power-user density
- reduce cognitive density
- density should come from structured information, not cramped layout or repeated chrome

## 15. Responsive Behavior

- primary navigation must remain available below `lg`
- standard list/detail pages may stack vertically on smaller screens
- action bars should wrap cleanly
- right rails should be opt-in or route-aware on smaller screens, not assumed permanent

## 16. Tabs Vs Sections Vs Accordions

- use tabs for peer views with mutually exclusive focus
- use sections for a single linear workflow
- use accordions for optional or rarely needed subsections
- do not use tabs when the user needs to compare sections simultaneously

## 17. Help / Info Hints

- hints should be compact and context-specific
- explanatory blocks should support the current task, not restate the system architecture
- reusable hint styling should not look like an error or warning unless it truly is one

## 18. Protected Workbench Rule

- on project structure and prompt factory routes, optimize the surrounding shell first
- do not use phase 1 to redesign the internals of the workbench stage

