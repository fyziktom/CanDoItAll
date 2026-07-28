# Package Versioning Decision

## Problem

The current branch repeats MAF versions in at least three project files. Stable and preview packages have different version formats. Manual edits can produce a mixed release train.

## Decision

Add one MAF-owned props file at `src/MAF/MicrosoftAgentFramework.Packages.props` and
explicitly import it from the three projects that own direct MAF references:

```xml
<PropertyGroup>
  <MicrosoftAgentsAIStableVersion>1.15.0</MicrosoftAgentsAIStableVersion>
  <MicrosoftAgentsAIPreviewVersion>1.15.0-preview.260722.1</MicrosoftAgentsAIPreviewVersion>
</PropertyGroup>
```

Use them only for direct MAF package references.

This keeps component dependency versions out of repository-wide
`Directory.Build.props`, in accordance with the CanDoItAll shared .NET ownership
standard.

## Why Not Enable Central Package Management Now

The repository has a large project graph and many non-MAF package versions. Enabling `ManagePackageVersionsCentrally` during this security-sensitive runtime migration would:

- expand scope to unrelated packages;
- alter restore behavior broadly;
- complicate rollback and review;
- obscure MAF-specific failures.

A future repository-wide dependency-management initiative can adopt Central Package Management separately.

## Adjacent Dependency Policy

Do not automatically change:

- `Microsoft.Extensions.AI 10.8.0`;
- `Microsoft.Extensions.AI.OpenAI 10.8.0`;
- `OpenAI 2.12.0`;
- `Azure.AI.OpenAI 2.9.0-beta.1`;
- `OllamaSharp 5.4.25`;
- `ModelContextProtocol 1.1.0`.

After restore:

1. inspect direct and transitive graphs;
2. resolve only actual conflicts/downgrades;
3. prefer the current newer direct versions when compatible;
4. document any necessary adjacent update as a separate change with its own tests.

## Package Gate

The package phase cannot close until:

- all direct MAF references use shared properties;
- stable packages resolve to 1.15.0;
- MAF A2A packages resolve to the matching preview build;
- no older transitive MAF assembly remains;
- no accidental Harness/AG-UI/declarative package is added;
- lock/assets graphs are attached to proof;
- target projects build before behavioral refactoring.
