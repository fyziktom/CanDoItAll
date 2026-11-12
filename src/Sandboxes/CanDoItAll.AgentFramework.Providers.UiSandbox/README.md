# Provider Profiles sandbox

The same full catalog and Connection, Prices, Runtime and Thinking renderers used by Web
run here with stateful local fixtures. No production DI, database, vault, source connection
or provider executor is registered. Sharing, History and source integrations are explicitly
deferred to the native host.

```powershell
dotnet run --project src/Sandboxes/CanDoItAll.AgentFramework.Providers.UiSandbox
```

Use a 1920×1080 desktop. The scenario selector covers realistic/loading/empty/large/missing,
partial secret metadata, source-managed, reconciliation warning, unknown result and held
operations. The second editor has a separate owner and draft. Held Save captures its request
before later input; release it with the explicit fixture control. Fixture outcomes are not
claims about native provider authority.

Build parity theme assets with `npm ci --prefix Tailwind` and `npm run tailwind:build` when
needed. Publish with `dotnet publish` and run the resulting DLL in Production from its output
directory to verify standalone assets. The Playwright provider sandbox theory performs both
source and published checks; the module/native journey verifies actual persistence separately.
