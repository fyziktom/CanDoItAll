# Plugins sandbox

Standalone host for the complete production Plugins renderer and presentation
policy. It registers Blazor and BaseLib only. No database, plugin implementation,
runtime loader, vault, OAuth transport, package installer or application restart
service is registered.

```powershell
dotnet watch --project src/Sandboxes/CanDoItAll.Plugins.UiSandbox --configuration PluginsUiProof
```

The scenario selector covers all six sections; empty/100-entry catalogs; missing
references/descriptors; unavailable, stale and partial reads; invalid fields;
held reads/saves/read-back/grants/OAuth/upload; refusal; saved-with-warning;
unknown writes; denied grants; and connected/reconnect/error OAuth states.
**Release pending operations** completes controlled waits. **Restore settings
reads** makes a read-only recovery. Scenario transitions retire the old workspace
and release its waits; an admitted fake write still commits to its original store.

Fake writes update separate storage before reads observe them. Connection IDs and
operation generations are real fixture identities. OAuth records a browser effect
without visiting a provider. Upload reads bounded harmless bytes and updates a
fake installed-package record; restart increments a request counter without
stopping the host. The sandbox does not claim real archive validation or database
persistence; production owner and browser tests cover those separately.

Parity mode links `src/App/CanDoItAll.Web/wwwroot/css/output.css`, which remains the
authoritative generated application stylesheet. When absent, run `npm ci --prefix
Tailwind` and `npm run tailwind:build` from the repository root. BaseLib provides
styles, material symbols, scripts and dialog behavior. The local SVG and controlled
package-icon route cover both icon forms in source and publish mode.

See [architecture and proof](../../../docs/architecture/plugins-ui-boundary.md).
