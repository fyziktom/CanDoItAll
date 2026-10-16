# Navigation state tests

Focused xUnit tests of the feature-neutral URL-state primitives in
[CanDoItAll.AppComponents.Navigation](../../../src/UI/CanDoItAll.AppComponents.Navigation/README.md):
query parsing, location composition, token maps, canonical writing, tolerant reading and the
`UrlStateBinding` history rules. A scripted `NavigationManager` delivers locations late, as the
interactive server host does, so in-flight and stale locations are exercised without a browser.
A scripted history answers Back and the state the browser keeps for an entry, optionally late, so
reloaded overlays and intents that race the host's answer are covered too.
These tests need no database or Web host.

```powershell
dotnet test tests/Unit/CanDoItAll.AppComponents.Navigation.Tests/CanDoItAll.AppComponents.Navigation.Tests.csproj --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Navigation." /m:1
dotnet test tests/Unit/CanDoItAll.AppComponents.Navigation.Tests/CanDoItAll.AppComponents.Navigation.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Navigation." /m:1
```

Compare source and discovered case counts before execution. See [Testing](../../../docs/testing.md).
