namespace CanDoItAll.AgentFramework.Core;

internal static class WorkspaceSpreadsheetPreviewScript
{
    public const string Content = """
import csv
import sys
from pathlib import Path

path = Path(sys.argv[1])
max_rows = max(1, int(sys.argv[2]))
max_cols = max(1, int(sys.argv[3]))
suffix = path.suffix.lower()

def normalize(value):
    if value is None:
        return ""
    if isinstance(value, float) and value.is_integer():
        value = int(value)
    text = str(value).replace("\r", " ").replace("\n", " ").strip()
    return text[:72] + "..." if len(text) > 72 else text

def print_rows(rows, label):
    print(f"{label}: {path.name}")
    preview = list(rows)
    if not preview:
        print("Spreadsheet is empty.")
        return
    for row in preview[:max_rows]:
        print("- " + " | ".join(normalize(cell) for cell in row[:max_cols]))

if suffix in {".csv", ".tsv"}:
    delimiter = "," if suffix == ".csv" else "\t"
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.reader(handle, delimiter=delimiter))
    print_rows(rows, "Spreadsheet")
elif suffix == ".xls":
    import xlrd
    book = xlrd.open_workbook(path.as_posix(), on_demand=True)
    try:
        print(f"Workbook: {path.name}")
        print("Sheets: " + ", ".join(book.sheet_names()))
        for sheet_name in book.sheet_names()[:2]:
            sheet = book.sheet_by_name(sheet_name)
            print()
            print(f"Sheet '{sheet_name}' preview:")
            if sheet.nrows == 0:
                print("No worksheet rows were found.")
                continue
            for row_index in range(min(sheet.nrows, max_rows)):
                row = [sheet.cell_value(row_index, column_index) for column_index in range(min(sheet.ncols, max_cols))]
                print("- " + " | ".join(normalize(cell) for cell in row))
    finally:
        book.release_resources()
else:
    from openpyxl import load_workbook
    workbook = load_workbook(path, read_only=True, data_only=True)
    print(f"Workbook: {path.name}")
    print("Sheets: " + ", ".join(workbook.sheetnames))
    for sheet_name in workbook.sheetnames[:2]:
        sheet = workbook[sheet_name]
        print()
        print(f"Sheet '{sheet_name}' preview:")
        rows = list(sheet.iter_rows(values_only=True, max_row=max_rows, max_col=max_cols))
        if not rows:
            print("No worksheet rows were found.")
            continue
        for row in rows:
            print("- " + " | ".join(normalize(cell) for cell in row[:max_cols]))
""";
}
