# CanDoItAll.Tools.Documents

## Purpose

Shared document helper library. Its current surface is spreadsheet inspection, cell/range reading, and workbook writing through ClosedXML-backed typed services.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Tools.Documents/CanDoItAll.Tools.Documents.csproj
```

## References

Project references:

- None

Framework references:

- None

Direct package references:

- `ClosedXML (0.105.0)`

## Architecture Notes

Keep document operations behind typed request/result contracts such as `SpreadsheetWriteRequest`, `SpreadsheetRangeReadResult`, and `ISpreadsheetDocumentService`. Callers should not manipulate ClosedXML objects directly outside this library.

The write path intentionally fails when the output file already exists. Preserve that explicit behavior so agents do not overwrite artifacts without a deliberate caller decision.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
