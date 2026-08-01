# CanDoItAll.AppComponents

## Purpose

Facade and application-owned component layer for shell and tab behavior, tuning
boundaries, record browsers and pickers, cards and filters, and FileTools host-action
and renderer adapters.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AppComponents.csproj](CanDoItAll.AppComponents.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep application-shell UI typed and focused. Reusable component behavior belongs in
the sibling `CanDoItAll.Components` repository. This facade consumes its BaseLib,
CanvasLib, and Common packages while owning app-specific pickers and cards plus adapters
that connect FileTools to shared components and host behavior. Image and sandboxed SVG
viewers adapt FileTools' object-URL targets to BaseLib's reusable zoom-pan frame; the SVG
target remains an inert sandboxed iframe. Add another dependency only when an
application-owned surface has a real need for it.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Shared UI boundary: `docs/ui-shared-components/README.md`
