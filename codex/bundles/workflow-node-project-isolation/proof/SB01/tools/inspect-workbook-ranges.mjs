import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const repoRoot = "C:/repositories/CanDoItAll";
const bundleRoot = path.join(repoRoot, "codex/bundles/workflow-node-project-isolation");
const workbookPath = path.join(bundleRoot, "inventories/workflow-node-project-isolation-map.xlsx");
const outputDir = path.join(bundleRoot, "proof/SB01/workbook-previews");

await fs.mkdir(outputDir, { recursive: true });

const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const ranges = [
  ["Project Targets", "A1:G30"],
  ["Executor Categories", "A1:G22"],
  ["Validation Matrix", "A1:G30"],
  ["Summary", "A1:H16"],
];

for (const [sheetId, range] of ranges) {
  const result = await workbook.inspect({
    kind: "region",
    sheetId,
    range,
    maxChars: 20000,
    tableMaxRows: 40,
    tableMaxCols: 10,
    tableMaxCellChars: 180,
  });

  const fileName = sheetId.replace(/[^a-z0-9]+/gi, "-").replace(/^-|-$/g, "").toLowerCase();
  await fs.writeFile(path.join(outputDir, `${fileName}-range.ndjson`), result.ndjson, "utf8");
}

console.log(`Inspected ${ranges.length} workbook ranges.`);
