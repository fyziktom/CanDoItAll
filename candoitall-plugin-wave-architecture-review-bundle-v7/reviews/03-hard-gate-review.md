# Hard-gate review

This review exists specifically because the same blockers repeated after previous refactor waves.

## Rule

A repeated blocker is not considered solved unless:
- the code changed
- tests were added or updated
- forbidden-pattern searches no longer match
- the hard-gate script passes

## Consequence

Future review bundles should treat any failed hard gate as a stop condition for the plugin wave.
