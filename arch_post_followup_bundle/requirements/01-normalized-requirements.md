# Normalized requirements

## R001
Reject self-loops and dependency cycles during save and publish.

## R002
Remove runtime/canvas silent fallbacks that currently compensate for illegal cyclic graphs.

## R003
Enforce runtime singularity in the database for step runs and run assignments.

## R004
Make `ProcessWorkspace` quiesce pending definition persistence before publish/delete/export and similar state-dependent actions.

## R005
Always provide definition concurrency metadata for existing definitions, including the no-draft path.

## R006
Provide a more cohesive workspace/run-details read boundary.

## R007
Centralize duplicated template mapping rules and make pack-thread-safety/caching decisions explicit.

## R008
Perform targeted scale and concentration cleanup only after correctness gaps are closed.

## R009
Do not close the bundle without fresh proof that covers the reopened scope.
