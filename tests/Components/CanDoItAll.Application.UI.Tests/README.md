# Application presentation tests

These tests render the production Application.UI leaves and their actual children without
Web, native modules or database services. They cover Dashboard states and links, full runtime
facts, profile dialog/flyout/status controls, separate views and captured callback identities.
The boundary checks inspect resolved dependencies, exported contracts and rendered descendants;
negative controls include unresolved and transitive references, native parameters and a native
child supplied through DynamicComponent.

Run the project in Release after rebuilding its production and scenario-host dependencies:

```powershell
dotnet test tests/Components/CanDoItAll.Application.UI.Tests/CanDoItAll.Application.UI.Tests.csproj --configuration Release /m:1
```

Real loading, caching, activation admission and late outcomes are tested in the native
Components assembly. Browser and process-restart proof live in the Playwright assembly.
See the [application seam](../../../src/UI/CanDoItAll.Application.UI/README.md) and the
[current closure evidence](../../../docs/architecture/components-decoupling-closure.md).
