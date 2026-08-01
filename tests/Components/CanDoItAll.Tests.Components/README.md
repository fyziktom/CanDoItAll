# CanDoItAll.Tests.Components

Exercises Blazor component rendering, interaction, module composition, dialogs, settings,
agent/workflow surfaces, and the local development manager integration.

```powershell
dotnet test .\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release
```

Tests use shared fixtures from `CanDoItAll.Tests.Support`. Browser-only behavior belongs
in the Playwright project.
