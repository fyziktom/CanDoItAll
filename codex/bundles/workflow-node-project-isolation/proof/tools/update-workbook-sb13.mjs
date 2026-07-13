import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbookPath = "codex/bundles/workflow-node-project-isolation/inventories/workflow-node-project-isolation-map.xlsx";
const previewDir = "codex/bundles/workflow-node-project-isolation/proof/SB13/workbook-previews";

const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);

function appendRows(sheetName, rows) {
  const sheet = workbook.worksheets.getItem(sheetName);
  const usedRange = sheet.getUsedRange();
  const startRow = usedRange.rowCount;
  const sourceRow = sheet.getRangeByIndexes(startRow - 1, 0, 1, usedRange.columnCount);

  rows.forEach((row, index) => {
    const target = sheet.getRangeByIndexes(startRow + index, 0, 1, usedRange.columnCount);
    target.copyFrom(sourceRow, "all");
    target.values = [row.slice(0, usedRange.columnCount)];
  });
}

appendRows("Summary", [[
  "SB13 adoption hardening checkpoint",
  "Implemented",
  "Guard tests, architecture/no-fallback audit, no-generic audit, performance scan, file-size review, component/integration/browser proof",
  "SB13",
  "Completed",
  "Critical",
  "SB14",
  "Added adoption-hardening guard tests, fixed stale SB09 MAF-folder expectation, proved typed diagnostics remain centralized, and repeated large-screen UI proof.",
]]);

appendRows("Source Map", [
  [
    "Adoption hardening guard tests",
    "tests\\CanDoItAll.Tests.Unit\\WorkflowAdoptionHardeningCheckpointTests.cs",
    "Unit tests",
    "Architecture/no-fallback and diagnostic-display regression guard",
    "SB13",
    "Hidden MAF fallback or raw message display can regress after adoption",
    "WorkflowAdoptionHardeningCheckpointTests passed 5/5 and combined hardening unit slice passed 37/37",
    "Completed",
  ],
  [
    "Executor hardening stale guard",
    "tests\\CanDoItAll.Tests.Unit\\WorkflowExecutorHardeningCheckpointTests.cs",
    "Unit tests",
    "Post-SB11 no-fallback expectation",
    "SB13",
    "Old MAF folder expectation contradicted SB11 adapter isolation",
    "Combined hardening unit tests passed after updating expectation to empty old MAF workflow folder",
    "Completed",
  ],
]);

appendRows("Project Targets", [[
  "Adoption hardening checkpoint",
  "No-fallback, no-generic-error, performance, and responsibility gate for API/UI/Workbench adoption",
  "SB12 adoption proof and isolated workflow/executor/template/adapter projects",
  "Deleting old paths or broad UI refactoring before hardening proof",
  "SB13",
  "SB14",
  "Architecture/no-fallback check, no-generic audit, file-size/responsibility review, performance scan, and large-screen browser proof",
]]);

appendRows("Subbundles", [[
  "SB13",
  "Adoption hardening checkpoint",
  "Prove live adoption and no fallback paths",
  "Hardening",
  "Critical",
  "SB12",
  "SB14",
  "Completed: added adoption guard tests, fixed stale executor-hardening expectation, repeated unit/component/integration/browser proof, and recorded performance/file-size/no-generic audits",
]]);

appendRows("Validation Matrix", [
  [
    "R11/R12/R13/R17/R18",
    "Adoption hardening",
    "API/UI/Workbench/host references use isolated services with no fallback and typed redacted diagnostics",
    "SB13",
    "Combined hardening unit tests 37/37, component 21/21, integration 46/46",
    "Large-screen Playwright workflow and Workbench proof passed",
    "0 critical performance findings; approved exception for pre-existing large UI files",
  ],
  [
    "R18",
    "File responsibility",
    "Adoption checkpoint prevents copied diagnostic parsing into UI/Workbench",
    "SB13",
    "WorkflowAdoptionHardeningCheckpointTests assert diagnostic deserialization stays in formatter",
    "N/A",
    "Large existing UI files documented as approved exception, not new SB13 monoliths",
  ],
]);

appendRows("Error States", [[
  "Adoption fallback regression",
  "API/UI/Workbench adoption surfaces",
  "No direct MAF workflow compiler/backend fallback, no raw event-message display, no generic workflow failure text",
  "SB13",
  "WorkflowAdoptionHardeningCheckpointTests, architecture-no-fallback check, no-generic-error audit, and anti-stub audit all passed",
  "Critical",
]]);

appendRows("Performance Signals", [[
  "Adoption hardening performance scan",
  "WorkflowFailureDisplayFormatter; WorkflowsApi; WorkflowsPage; WorkflowCanvasEditor; ProjectStructureWorkflowNodeService",
  "0 critical findings; existing UI/API LINQ/list candidates are not new executor/runtime hot loops; static JsonSerializerOptions helper hits are cached field initializers",
  "Avoid speculative UI rewrite without profiling",
  "SB13/SB14",
  "SB13 performance-scan.txt; SB14 final scan must re-check before completed closure",
]]);

await fs.mkdir(previewDir, { recursive: true });

for (const sheet of workbook.worksheets.items) {
  const preview = await workbook.render({
    sheetName: sheet.name,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });

  const safeName = sheet.name.replace(/[^a-z0-9]+/gi, "-").toLowerCase();
  await fs.writeFile(`${previewDir}/${safeName}.png`, new Uint8Array(await preview.arrayBuffer()));
}

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});

console.log(errors.ndjson);

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(workbookPath);
console.log(`Updated ${workbookPath}`);
console.log(`Rendered previews to ${previewDir}`);
