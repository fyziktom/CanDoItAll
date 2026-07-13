import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbookPath = "codex/bundles/workflow-node-project-isolation/inventories/workflow-node-project-isolation-map.xlsx";
const previewDir = "codex/bundles/workflow-node-project-isolation/proof/SB12/workbook-previews";

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
  "SB12 API/UI/Workbench adoption",
  "Implemented",
  "Unit, integration, component, static, and large-screen Playwright proof",
  "SB12",
  "Completed",
  "Critical",
  "SB13",
  "Workflow UI, canvas editor, Workbench workflow nodes, and agent-tool status paths now display typed redacted workflow diagnostics through WorkflowFailureDisplayFormatter; small/medium UI proof intentionally skipped per large-screen-only instruction.",
]]);

appendRows("Source Map", [
  [
    "Workflow UI diagnostic display",
    "src\\CanDoItAll.Modules.AgentFramework\\Pages\\WorkflowsPage*; WorkflowCanvasEditor.razor.cs",
    "Blazor module",
    "Consumes workflow-owned failure diagnostics through formatter",
    "SB12",
    "Raw exception/event messages could leak secrets or lose repair context",
    "WorkflowsPageTests typed failure display; WorkflowShellSmokeTests large-screen browser proof",
    "Completed",
  ],
  [
    "Workbench workflow status diagnostics",
    "src\\CanDoItAll.Modules.Workbench\\ProjectStructure\\ProjectStructureWorkflowNodeService.cs; ProjectStructurePage.WorkflowNodes.cs",
    "Workbench module",
    "Consumes workflow runtime events and typed diagnostics through isolated runtime/core contracts",
    "SB12",
    "Workflow-node status could regress to string-only failures",
    "ProjectStructureWorkflowPreviewSimulationSupportTests; Playwright workflow-node large-screen proof",
    "Completed",
  ],
]);

appendRows("Project Targets", [[
  "API/UI/Workbench diagnostic adoption",
  "User-safe workflow failure display and status shaping",
  "Workflows.Core, Workflows.Runtime, Workflows.Templates, executor contracts",
  "MAF internals or exception-string parsing as recovery path",
  "SB12",
  "SB13",
  "Typed event diagnostic tests, component display tests, integration tests, and large-screen Playwright proof",
]]);

appendRows("Subbundles", [[
  "SB12",
  "API/UI/Workbench adoption",
  "Adopt isolated services in user-facing surfaces",
  "Adoption",
  "Critical",
  "SB11",
  "SB13",
  "Completed: API/workbench integration, workflow UI component tests, typed diagnostic display, no-MAF-fallback static check, and large-screen Playwright workflow/workbench proof",
]]);

appendRows("Plugin Consequences", [[
  "Plugin/UI failure display",
  "WorkflowFailureDisplayFormatter and WorkflowsPage event display",
  "Plugin/runtime executor diagnostics surfaced through workflow event payload envelopes",
  "SB12",
  "Completed: failed workflow/plugin/template diagnostic display uses typed user-safe messages and formatter redaction rather than raw event strings.",
  "Critical",
  "SB13 rechecks no fallback/string parsing under adoption hardening",
]]);

appendRows("Validation Matrix", [
  [
    "R12/R17",
    "API/UI/Workbench adoption",
    "User-facing workflow and Workbench surfaces consume isolated services and typed diagnostics",
    "SB12",
    "Unit/component/integration tests passed",
    "Large-screen Playwright workflow shell and Workbench workflow-node proof passed",
    "Small and medium viewport tests skipped per large-screen-only instruction",
  ],
  [
    "R15",
    "Artifact-backed adoption proof",
    "Manifest, semantic invariants, hashes, static audit, and browser screenshots exist",
    "SB12",
    "proof/SB12 transcripts and changed-file hashes",
    "proof/SB12/browser screenshots",
    "Prepared/completed validator re-run required after bundle doc sync",
  ],
]);

appendRows("Error States", [[
  "UI/API/Workbench workflow display failure",
  "Workflow UI and Workbench workflow nodes",
  "Typed user message, repair hint when present, node/executor/plugin/tool context when present, redacted technical detail",
  "SB12/SB13",
  "SB12 typed diagnostic unit/component tests and static formatter adoption check; SB13 no-generic-error hardening remains",
  "Critical",
]]);

appendRows("Performance Signals", [[
  "Adoption diagnostic formatting",
  "WorkflowFailureDisplayFormatter; WorkflowsPage; ProjectStructureWorkflowNodeService",
  "Formatter centralizes event diagnostic parsing/redaction instead of repeated UI-side string parsing",
  "Repeated deserialization is limited to failure display paths; SB13 performance scan rechecks adoption hot paths",
  "SB12/SB13",
  "SB12 implementation keeps logic in shared formatter; SB13 must scan descriptor/template/UI shaping paths",
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
