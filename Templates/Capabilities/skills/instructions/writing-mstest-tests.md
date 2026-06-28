# Writing MSTest Tests Internal Agent Skill

Use this skill when an internal agent writes or reviews MSTest validation.

Work rules:

- Test observable behavior and contracts, not private implementation details.
- Keep test setup small and local to the behavior under test.
- Use clear Arrange/Act/Assert structure without excessive comments.
- Cover negative paths when the feature has explicit validation or failure behavior.
- Avoid broad sleeps, real network dependencies, and order-dependent assertions.

For .NET work, make sure the selected test framework matches the existing test project.
