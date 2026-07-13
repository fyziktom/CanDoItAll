import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbookPath = "codex/bundles/workflow-node-project-isolation/inventories/workflow-node-project-isolation-map.xlsx";
const previewDir = "codex/bundles/workflow-node-project-isolation/proof/SB14/workbook-previews";

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
  "SB14 final regression cleanup and docs",
  "Implemented",
  "Final cleanup proof, documentation, static audits, unit/component/integration/large-screen browser proof, workbook update, and completed validator",
  "SB14",
  "Completed",
  "Critical",
  "Closed",
  "Closed the workflow-node project isolation initiative with artifact-backed proof and final developer conventions.",
]]);

appendRows("Source Map", [
  [
    "Final workflow hardening guidance",
    "docs\\workflow-maf-hardening.md",
    "Documentation",
    "Current workflow/executor/template/plugin ownership and diagnostic/file-responsibility rules",
    "SB14",
    "Future contributors could reintroduce old MAF ownership or duplicated diagnostics without a current guidance document",
    "Documentation review passed and completed validator proof recorded under proof\\SB14",
    "Completed",
  ],
  [
    "SB14 performance cache fixes",
    "src\\CanDoItAll.AgentFramework.WorkflowExecutors.Core\\WorkflowExecutorObservability.cs; src\\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure\\ProjectStructureWorkflowTaskNodes.cs",
    "Workflow executor runtime source",
    "Cached serializer options in redaction and task metadata paths",
    "SB14",
    "Per-call serializer option allocations in runtime-adjacent paths",
    "Focused performance scan passed with 0 critical findings",
    "Completed",
  ],
  [
    "Final hardening guard alignment",
    "tests\\CanDoItAll.Tests.Unit\\WorkflowFoundationHardeningCheckpointTests.cs; tests\\CanDoItAll.Tests.Unit\\WorkflowRuntimeExtractionTests.cs; tests\\CanDoItAll.Tests.Unit\\WorkflowExecutorFoundationExtractionTests.cs",
    "Unit tests",
    "Final project reference and composition guard expectations",
    "SB14",
    "Stale guard expectations could hide the real finalized boundary or fail valid composition",
    "Focused unit regression passed 128/128 after guard alignment",
    "Completed",
  ],
]);

appendRows("Project Targets", [[
  "Final workflow/executor ownership",
  "Models, Workflows.Abstractions/Builder/Core/Runtime/Templates/MafAdapter, WorkflowExecutors.Abstractions/Core/Standard/Plugins, and UI/API/Workbench adoption surfaces",
  "Completed SB01-SB13 implementation and SB14 final proof",
  "Reintroducing MAF-owned workflow implementation, default executor fallback registration, UI-owned template parsing, or duplicated diagnostic parsing",
  "SB14",
  "Closed",
  "Static no-fallback/no-generic audits, final regression tests, documentation review, and completed-stage validator",
]]);

appendRows("Subbundles", [[
  "SB14",
  "Regression proof cleanup and docs",
  "Close initiative with final proof and developer conventions",
  "Closure",
  "Critical",
  "SB13",
  "Closed",
  "Completed: obsolete path absence proved, docs updated, performance cache fixes applied, unit/component/integration/browser proof passed, workbook rendered, and completed validator recorded",
]]);

appendRows("Validation Matrix", [
  [
    "R01-R18",
    "Final closure",
    "All raw request themes have artifact-backed proof and final status",
    "SB14",
    "Unit 128/128, component 21/21, integration 65/65",
    "Large-screen workflow shell and Workbench Playwright tests passed; small/medium skipped by request",
    "Completed-stage validator and proof manifests close the initiative",
  ],
  [
    "R17",
    "Typed diagnostics final regression",
    "Workflow/runtime/executor/plugin/template/MAF/API/UI/Workbench paths keep typed redacted repairable diagnostics",
    "SB14",
    "No-generic/redaction audit and focused unit/component/integration tests passed",
    "Large-screen browser paths passed",
    "Legacy text fallback remains only inside WorkflowFailureDisplayFormatter",
  ],
  [
    "R18",
    "File responsibility final review",
    "Moved workflow/executor/template/adapter code has focused owners; existing large UI/Workbench orchestration files are documented exceptions",
    "SB14",
    "File-size/responsibility review passed",
    "N/A",
    "New parsing/diagnostics/runtime/template/executor behavior must stay outside approved large files",
  ],
]);

appendRows("Error States", [[
  "Final diagnostic regression",
  "Runtime, default executors, plugin executors, templates, MAF adapter, API, UI, Workbench, external tool/MCP paths",
  "Typed failure envelope, repair hint, retryability, redacted technical detail, and source context required; no generic error text or silent fallback",
  "SB14",
  "No-generic/redaction audit, final regression suite, and documentation review passed",
  "Critical",
]]);

appendRows("Performance Signals", [[
  "Final focused workflow/executor performance scan",
  "Isolated workflow, executor, template, MAF adapter, and bundled plugin source paths",
  "0 critical findings after caching redacted JSON and project-structure task metadata serializer options",
  "No broad optimization without measured hot-path evidence",
  "SB14",
  "proof\\SB14\\transcripts\\performance-scan.txt",
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
