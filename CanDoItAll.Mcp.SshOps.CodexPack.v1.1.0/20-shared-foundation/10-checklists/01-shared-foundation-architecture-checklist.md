# Shared foundation architecture checklist

- [ ] Existují projekty `CanDoItAll.Mcp.Core` a `CanDoItAll.Mcp.LocalRuntime`.
- [ ] Shared foundation neobsahuje dotnet watch doménovou logiku.
- [ ] Shared foundation neobsahuje SSH / Docker / Traefik / PostgreSQL / IPFS doménovou logiku.
- [ ] Shared foundation nemá zakázané project references.
- [ ] Shared contracts jsou dost malé a stabilní.
- [ ] Observability primitives jsou sdílené a netvoří duplicity.
- [ ] Local child-process runtime je oddělený od Core.
- [ ] Boundary rules jsou zdokumentované.
