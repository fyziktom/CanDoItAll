# Partial Class Policy

Partial classes are a language feature, not an architectural boundary.

## Allowed uses

Use partial classes only when one of these applies:

1. Generated code or source-generator extension points.
2. UI component code-behind where the component remains cohesive.
3. Platform-specific interop where each partial file maps to a clearly named platform slice.
4. Temporary migration scaffold with an explicit removal subbundle and proof that the final design will not depend on the partial split.
5. Extremely small syntax-level separation where the class still has one responsibility and no better project or type boundary exists.

## Blocked uses

Do not use partial classes to hide a large class problem.

Blocked patterns:

- `Runtime.Tools.cs`, `Runtime.Memory.cs`, `Runtime.Plugins.cs`, `Runtime.Mcp.cs`, and similar files that keep every responsibility inside the same runtime type.
- Partial files that define nested provider, tool, strategy, or handler classes.
- Partial files that own unrelated dependency registration, catalog construction, orchestration, persistence, and validation in the same type.
- Partial files that make unit tests depend on constructing the original large runtime.
- Partial files added because extracting abstractions would require dependency analysis.

## Required response when Codex wants a new partial

Before adding a new partial class file, Codex must answer:

- What responsibility is being separated?
- Why is a new top-level type not better?
- Why is a new project not better?
- Which tests will exercise the extracted behavior without constructing the original runtime?
- What prevents the original class from continuing to grow?
- Is the partial file temporary? If yes, which subbundle removes it?

If the answer is weak, create a new top-level type, project, builder, factory, strategy, provider, or adapter instead.
