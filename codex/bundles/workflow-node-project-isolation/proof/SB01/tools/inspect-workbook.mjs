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

const summary = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 12000,
  tableMaxRows: 8,
  tableMaxCols: 8,
  tableMaxCellChars: 120,
});

await fs.writeFile(path.join(outputDir, "workbook-summary.ndjson"), summary.ndjson, "utf8");

const sheetInspection = await workbook.inspect({
  kind: "sheet",
  include: "id,name",
  maxChars: 12000,
});

await fs.writeFile(path.join(outputDir, "sheet-list.ndjson"), sheetInspection.ndjson, "utf8");

const sheets = [];
for (const line of sheetInspection.ndjson.split(/\r?\n/)) {
  if (!line.trim()) {
    continue;
  }

  const item = JSON.parse(line);
  if (item.name) {
    sheets.push(item.name);
  }
}

for (const sheetName of sheets) {
  const preview = await workbook.render({
    sheetName,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });

  const fileName = sheetName.replace(/[^a-z0-9]+/gi, "-").replace(/^-|-$/g, "").toLowerCase();
  const bytes = new Uint8Array(await preview.arrayBuffer());
  await fs.writeFile(path.join(outputDir, `${fileName}.png`), bytes);
}

const errorScan = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "SB01 workbook formula error scan",
});

await fs.writeFile(path.join(outputDir, "formula-error-scan.ndjson"), errorScan.ndjson, "utf8");

console.log(`Rendered ${sheets.length} sheets to ${outputDir}`);
