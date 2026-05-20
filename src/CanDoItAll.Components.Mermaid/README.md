# CanDoItAll.Components.Mermaid

## Purpose

Shared Razor Mermaid diagram component with source normalization, render result models, render error models, node-click event arguments, and Mermaid head assets.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Components.Mermaid/CanDoItAll.Components.Mermaid.csproj
```

## References

Project references:

- None

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.4)`

## Architecture Notes

Use this component for product-rendered Mermaid diagrams. Keep syntax guidance and authoring assistance in `CanDoItAll.Mcp.Mermaid`; this library should stay focused on rendering, options, normalization, and typed UI events.

## Related Docs

- Repository overview: `README.md` at the repo root
- Shared components: `docs/ui-shared-components/README.md`
