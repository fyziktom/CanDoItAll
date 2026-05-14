# Check architecture and source-of-truth impact

Confirm canonical models, integration boundaries, persistence ownership, and UI/application/domain responsibilities before code starts.

Use the current project-structure mindmap and upstream slice scope packet as the source of truth. Copy explicit product root, app archetype, solution/project names, target framework, test framework, argument meanings, feature list, exclusions, and validation hooks exactly; treat explicit facts as resolved decisions rather than unresolved questions, and do not add optional behavior or substitute preferred defaults.

This is not a build, startup, or browser validation step. For a greenfield product root that does not exist yet, record the root as the intended setup target and return a completed architecture disposition when source-of-truth ownership and boundaries are clear. Block only on contradictory architecture requirements, unsafe ownership leakage, missing mandatory upstream evidence, or an unavailable writable path for the architecture artifact.
