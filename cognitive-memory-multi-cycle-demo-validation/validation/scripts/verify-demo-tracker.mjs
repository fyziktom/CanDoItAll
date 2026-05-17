import path from "node:path";
import { fileURLToPath } from "node:url";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const bundleRoot = path.resolve(scriptDir, "..", "..");
const trackerPath = path.join(bundleRoot, "sample-data", "trackers", "cognitive-memory-demo-source-tracker.xlsx");

const input = await FileBlob.load(trackerPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const manifestPreview = await workbook.inspect({
  kind: "table",
  range: "Source Manifest!A1:L8",
  include: "values,formulas",
  tableMaxRows: 8,
  tableMaxCols: 12
});

const formulaErrors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
  summary: "formula error scan"
});

for (const sheetName of ["Source Manifest", "Cycle Plan", "Chat Probes", "Memory Analysis", "Repair Log"]) {
  await workbook.render({ sheetName, range: "A1:H12", scale: 1 });
}

console.log(JSON.stringify({
  trackerPath,
  manifestPreview: manifestPreview.ndjson.split("\n").slice(0, 8),
  formulaErrors: formulaErrors.ndjson
}, null, 2));
