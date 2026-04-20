# Migration and pilot criteria

## This bundle does not decide product adoption

The concept may still fail. A future pilot on the real Processes workspace should only be considered if the concept proves the items below.

## Pilot-entry criteria

- dense templates are measurably easier to inspect in at least some important cases,
- labels stay readable without awkward camera work,
- move/connect authoring remains understandable,
- the automation bridge proves real state changes,
- the universal library boundary stayed generic,
- the dedicated sandbox did not need production persistence to demonstrate value.

## Hard blockers

- persistent label occlusion,
- proof still depends on manual-only screenshots,
- the library became process-specific,
- authoring interactions require per-frame server round trips,
- the concept only works on trivial templates.

## Recommended follow-on if the concept succeeds

1. pilot one narrow read-only comparison inside the Processes module,
2. keep WebGL optional beside the existing 2D workbench,
3. only later explore persistence-backed editing on a limited subset of semantics.

## Recommended follow-on if the concept fails

- preserve the bundle as decision evidence,
- capture the specific readability or automation reasons for failure,
- keep improving the current 2D workbench instead of forcing a poor 3D path.
