# TestLab renderer and scenario tests

A light bUnit lane referencing the standalone sandbox. It exercises actual controls,
before-blur input, invalid raw timestamps, section remounting, duplicate form submission,
fake storage versus selected draft, delayed completion and disposal. Dependency tests
traverse the complete renderer/sandbox assembly closure, reject forbidden/unresolved edges
and inspect exported contract types and renderer injection.

```powershell
dotnet test tests/Components/CanDoItAll.TestLab.UI.Tests/CanDoItAll.TestLab.UI.Tests.csproj --configuration Release
```

Included in Components and Stable solutions and the Components/Integration CI shards.
Real production host tests stay in CanDoItAll.Tests.Components and Playwright.
