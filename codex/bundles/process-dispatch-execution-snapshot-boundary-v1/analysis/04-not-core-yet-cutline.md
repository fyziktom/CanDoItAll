# Not Core Yet Cutline

This bundle is allowed to add neutral contracts and small helper services that make future core extraction safer. It is not allowed to declare or implement the final `Processes.Core` boundary.

Allowed:

- Extend `CanDoItAll.Processes.Contracts` with neutral snapshots.
- Add process-module-local mapper/client/helper classes.
- Add architecture tests that prove contracts neutrality and dispatcher dependency reduction.
- Add focused tests for execution/failure/receipt mapping.

Not allowed:

- Creating `CanDoItAll.Processes.Core`.
- Moving EF entities or persistence models.
- Moving dispatcher finalization, artifact validation, grounding, browser proof, or domain-specific code wholesale.
- Adding driver-pack abstractions.
