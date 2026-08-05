# Writing MSTest Tests Internal Agent Skill

Use this skill when an internal agent writes or reviews MSTest validation.

Work rules:

- Test observable behavior and contracts, not private implementation details.
- Keep test setup small and local to the behavior under test.
- Use clear Arrange/Act/Assert structure without excessive comments.
- Cover negative paths when the feature has explicit validation or failure behavior.
- Avoid broad sleeps, real network dependencies, and order-dependent assertions.
- Choose one test runner for each test project before scaffolding and keep it consistent. Do not mix MSTest, xUnit, and NUnit packages or convert the generated project to another runner in the same attempt.
- When the selected scaffold is `dotnet new mstest` or the generated project uses the `MSTest` meta-package, preserve its generated package family and versions unless restore diagnostics prove a concrete replacement is required.
- Use the analyzer-approved assertion surface exposed by the generated MSTest package. Prefer `Assert.Throws<T>` and `Assert.ThrowsExactly<T>` for exception contracts, and use collection assertions such as `Assert.HasCount(expected, collection)` when the installed API provides them.
- Reject legacy `Assert.ThrowsException` and `[ExpectedException]` patterns. Do not weaken analyzer-approved assertions merely to reuse an older example.
