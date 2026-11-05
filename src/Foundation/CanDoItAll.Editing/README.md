# Editing

Pure typed three-way comparison and transport-neutral change-hint contracts. This project
has no package or project references. Owners retain authorization, original-state write
conditions, lifecycle validation, and accepted-state reconciliation.

Scalar choices, normalized membership and keyed rows preserve domain equality. Text
comparison uses a linear, conservative enclosing hunk per side. Disjoint hunks may combine;
overlap or configured bounds require explicit review, preserving the entire input. No
external diff dependency or license is introduced. Review choices never persist data.
Owners can require a new choice when a refreshed current value invalidates an explicit
earlier decision; the original comparison baseline remains available.

Hint cursors describe delivery only. Implementations must bound subscriptions and buffers;
the application must authorize each subscription and retire it with its opening/context.
