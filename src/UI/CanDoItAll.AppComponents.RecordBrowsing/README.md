# CanDoItAll.AppComponents.RecordBrowsing

## Purpose

The application's server-paged record browser family: `PagedRecordBrowser`, `PagedRecordPickerDialog`
and their request, page, option, filter, selection and template contracts. The family is
feature-neutral; a consumer supplies a loader delegate and option projections. The types keep the
`CanDoItAll.AppComponents` namespace, and
[CanDoItAll.AppComponents](../CanDoItAll.AppComponents/README.md) references this project, so every
consumer of AppComponents still sees the family.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`

Validation command, from the repository root:

    dotnet build src/UI/CanDoItAll.AppComponents.RecordBrowsing/CanDoItAll.AppComponents.RecordBrowsing.csproj

## Dependencies

The authoritative dependency list is in
[CanDoItAll.AppComponents.RecordBrowsing.csproj](CanDoItAll.AppComponents.RecordBrowsing.csproj):
`CanDoItAll.Components.BaseLib` and `Microsoft.AspNetCore.Components.Web`. It exists so a
lightweight feature UI can compose the browser without AppComponents' canvas, file and
conversation dependencies.

The neutral `ResourceCardPicker` and `SelectedReferenceTable` families also live here.
Their namespaces and public types are unchanged; AppComponents forwards the old assembly
types. StorageSelection and the Agent editor reuse these actual controls without acquiring
AppComponents' runtime dependencies. Their scoped styles travel with this Razor library.

## Controlled query

`PagedRecordBrowser` keeps its own search, tags, filter and page unless the owner passes `Query`
(a `PagedRecordQueryState`, for example the page URL's catalog state). Then the browser applies
that query when it starts and whenever the owner supplies a different one, such as after Back or
Forward, and reports each committed change once through `QueryChanged` with its kind: `Search`
after the debounce, `Tags`, `Filter`, `Reset`, `Page`, and `Correction` when a requested page lies
beyond the last one and the browser loads the last page instead. What the operator is still typing
is never reported, and the owner handing back the browser's own commit loads nothing.
`MaximumSearchLength` bounds the search box like the source does. Without `Query` the browser
behaves as before and never corrects a page.
