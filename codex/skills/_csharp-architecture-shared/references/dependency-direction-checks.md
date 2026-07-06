# Dependency Direction Checks

Use these checks before adding or changing project references.

## Required questions

- Which project currently owns the contract?
- Which project owns the implementation?
- Does the caller need a concrete implementation or only an interface?
- Does a core/abstractions project reference infrastructure, UI, module, provider, persistence, or SDK-specific code?
- Would the new reference create a cycle?
- Can shared records or interfaces move to a smaller Contracts or Abstractions project?
- Is the composition root the only place that needs to reference both abstraction and implementation?

## Useful commands

The exact command may vary by repository, but Codex should collect equivalent proof:

```bash
dotnet list <project-or-solution> reference
dotnet build <solution-or-project>
dotnet test <test-project-or-solution>
```

For textual audits:

```bash
rg "partial class|IServiceProvider|BuildServiceProvider|new .*Client|new .*Provider" src tests
rg "ProjectReference" src tests -g "*.csproj"
```

On Windows PowerShell, a quick large-class scan can be done with:

```powershell
Get-ChildItem src -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    $lines = (Get-Content $_.FullName).Count
    if ($lines -gt 400) { [PSCustomObject]@{ Lines = $lines; Path = $_.FullName } }
  } |
  Sort-Object Lines -Descending
```

## Red flags

- Core references Provider, Persistence, UI, or Modules.
- Abstractions references concrete implementation.
- New project reference added only to access one DTO.
- Runtime references every provider implementation directly.
- Tests require the full app host for a pure policy or builder behavior.
- A cycle is "fixed" by moving everything into a common project.
