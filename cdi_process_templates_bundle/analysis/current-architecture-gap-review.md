# Current architecture gap review

## Observed current capabilities
The current process module already supports explicit dependencies, artifact inputs, decision role requirements, branch nodes, branch outcomes, and branch coordinates.

## Why the original bundle became stale
The original bundle still reflected an older architectural moment. That meant several process designs were slightly flattened or simplified to match the older constraint set.

## Architectural conclusion
The correct direction is not to preserve those simplifications. The correct direction is to use the newer graph features directly, then keep older limitations visible only where they still genuinely exist.
