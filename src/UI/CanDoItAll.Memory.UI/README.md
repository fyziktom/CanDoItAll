# Memory UI

The Razor class library renders the complete seven-tab Memory workspace, including every
provider, transport editor, ledger, context pack and provider UI descendant. It depends
on Memory.Contracts, Memory.Abstractions and the existing BaseLib/Common components.
It has no module, Application, transport, persistence or Web reference.

`MemoryWorkspaceSurface` receives `IMemoryWorkspace`; production and the sandbox use the
same presentation controller. Scoped CSS belongs to its `.memory-workspace` DOM root.
The registered-component/approved-URL provider UI boundary is explicit; the base library
never loads an assembly or resolves a service from a manifest.

See [the boundary and proof record](../../../docs/architecture/memory-ui-boundary.md).
