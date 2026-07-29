# CanDoItAll.Tools.Documents

## Purpose

Shared document helper library. Its current surface covers managed document-to-Markdown
conversion plus ClosedXML-backed spreadsheet inspection, bounded previews, cell/range
reading, and workbook writing.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Tools/CanDoItAll.Tools.Documents/CanDoItAll.Tools.Documents.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Tools.Documents.csproj](CanDoItAll.Tools.Documents.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep document operations behind typed request/result contracts such as
`WorkspaceDocumentMarkdownConversionRequest`, `SpreadsheetWriteRequest`,
`SpreadsheetRangeReadResult`, and `ISpreadsheetDocumentService`. Callers should not
manipulate ClosedXML objects directly outside this library.

The spreadsheet write path rejects an existing output file unless the caller explicitly
sets `SpreadsheetWriteRequest.Overwrite` to `true`; with that option enabled, the output
is replaced.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
