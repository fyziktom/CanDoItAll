# CanDoItAll.AppComponents.Navigation

## Purpose

Feature-neutral URL-state primitives for routed pages: typed query reading and canonical
writing, stable token maps, the per-page state codec contract, location composition that
keeps foreign query keys and the fragment, and `UrlStateBinding`, which keeps one page's
state and its URL in step with explicit Push and Replace history.

Feature modules own their codecs, tokens and reducers. This project never names a feature
key or token. [Navigation state](../../../docs/architecture/navigation-state.md) is the
canonical guidance for using it.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`

Validation command, from the repository root:

    dotnet build src/UI/CanDoItAll.AppComponents.Navigation/CanDoItAll.AppComponents.Navigation.csproj

## Dependencies

The authoritative dependency list is in
[CanDoItAll.AppComponents.Navigation.csproj](CanDoItAll.AppComponents.Navigation.csproj):
`Microsoft.AspNetCore.Components.Web` only. It is a separate leaf so every module that owns
a routed page, and a contracts assembly that publishes entry links, can use it without
AppComponents' canvas, file and conversation dependencies.

## Architecture Notes

- `IUrlStateCodec<TState>` declares every query key a page owns, reads a tolerant and
  normalized state, and writes the canonical form with defaults omitted.
- `UrlLocation.Compose` replaces only owned keys; foreign keys keep their original text and
  the fragment is preserved.
- `UrlStateBinding<TState>` applies the location in `OnParametersSet`, canonicalizes with
  one Replace, applies user intents immediately and ignores its own in-flight locations.
  While prerendering it writes nothing; given the page type it also ignores, and never
  writes onto, a location outside the page's routes (`UrlRoutes` matches `@page` templates).
- `OpenOverlay` marks the pushed history entry with its parent; `CloseOverlayAsync` goes
  Back to that parent when the marker matches and otherwise replaces the location. Writes made
  while that Back is in flight are held until it arrives, because a browser drops an entry
  pushed before a pending Back runs.
- `IAppNavigationHistory` is the host seam for Back and for the state of the current entry,
  which Blazor does not report after a reload. `AddAppNavigation` registers the browser
  implementation (it reads `history.state` through .NET 10 JS interop); a native host can
  register its own, and one that cannot tell the entry state keeps the default `null`.

## Related Docs

- Repository overview: `README.md` at the repo root
- Navigation state: `docs/architecture/navigation-state.md`
- UI component seams: `docs/architecture/ui-component-seams.md`
