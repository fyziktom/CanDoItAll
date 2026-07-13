import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbookPath = "codex/bundles/workflow-node-project-isolation/inventories/workflow-node-project-isolation-map.xlsx";
const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const sheets = await workbook.inspect({
  kind: "sheet",
  include: "id,name,range,index",
  maxChars: 12000,
});

console.log(sheets.ndjson);

for (const sheet of workbook.worksheets.items) {
  const usedRange = sheet.getUsedRange();
  const rowCount = usedRange.rowCount;
  const colCount = usedRange.columnCount;
  const startRow = Math.max(0, rowCount - 8);
  const tail = sheet.getRangeByIndexes(startRow, 0, rowCount - startRow, colCount);
  const inspected = await workbook.inspect({
    kind: "region",
    sheetId: sheet.name,
    range: tail.address,
    maxChars: 6000,
    tableMaxRows: 12,
    tableMaxCols: 12,
    tableMaxCellChars: 120,
  });

  console.log(`-- ${sheet.name} ${tail.address}`);
  console.log(inspected.ndjson);
}
