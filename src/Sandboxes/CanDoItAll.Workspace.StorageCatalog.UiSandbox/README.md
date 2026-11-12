# Storage catalog UI sandbox

Run `dotnet watch --project src/Sandboxes/CanDoItAll.Workspace.StorageCatalog.UiSandbox`.
The exact production renderer uses a bounded mutable scenario store. Choose a scenario and
Reset, or hold/fail/release individual read and effect stages. Retired admitted work stays
with its original store; release it through the retained-store control. At most four retired
stores are retained, and reset refuses when that capacity is occupied.

Health is simulated. Recovery is reported as a deferred host callback. There is no database,
vault, network driver, production module, Core or API leaf reference. Theme CSS is content
linked, not a Web project dependency. Publish normally and run the produced DLL in Production.
See the [boundary record](../../../docs/architecture/workspace-storage-catalog-ui-boundary.md).
