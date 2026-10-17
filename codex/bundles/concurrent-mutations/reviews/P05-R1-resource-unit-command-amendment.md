# Resource Unit fixture: explicit immutable command

The parent-owned Unit Release build reported CS0121 at the governed connector
pre-persistence test. `ResourceMetadataPreparation.Prepare` now has two overloads;
the target-typed `new(...)` did not identify the intended command.

The parent replaced that one construction with
`new ReplaceResourceMetadataCommand(original, project, ResourceMetadataValues.Capture(model))`.
The managed mirror was guarded by exact before/after hashes, and replacing only that
expression in the preserved predecessor reproduces the complete successor. No API,
production behavior, test arrangement, assertion or expected outcome changes.

The file remains six Facts plus one four-row InlineData theory: ten rows. The union
remains 346 ordinary, with stage1 fourteen first and four browser cases separate.
Original Resource inventory4947, runner5062 and ordinary V2 inventory3FC839 are
historical; the next ordinary/browser derivatives must explicitly bind this source
overlay after the parent finishes compile repairs. Do not silently update their
existing selection files.

Predecessor3B122CE9 and original Resource inventory are retained under
`P05-R1-resource-unit-command-predecessors`, together with the parent's exact change
receipt. Successor44AB450D is the parent-authored source mirrored into managed.
No build, test, database operation or MAIN source change was performed by this agent.
Parent retains the failed build and owns the subsequent standard Release build,
source/binary freeze, discovery, runtime proof and static enforcement.
