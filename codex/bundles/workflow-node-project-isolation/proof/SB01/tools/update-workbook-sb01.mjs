import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const repoRoot = "C:/repositories/CanDoItAll";
const bundleRoot = path.join(repoRoot, "codex/bundles/workflow-node-project-isolation");
const workbookPath = path.join(bundleRoot, "inventories/workflow-node-project-isolation-map.xlsx");

const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);

function writeRows(sheetName, rangeAddress, rows) {
  const sheet = workbook.worksheets.getItem(sheetName);
  const range = sheet.getRange(rangeAddress);
  range.values = rows;
  range.format = {
    wrapText: true,
    borders: { preset: "all", style: "thin", color: "#D9E2F3" },
  };
}

writeRows("Source Map", "A29:H31", [
  [
    "Cognitive Memory workflow executors",
    "src\\CanDoItAll.Modules.CognitiveMemory\\Advanced\\CognitiveMemoryMafIntegration.cs; CognitiveMemoryModuleServiceCollectionExtensions.cs",
    "CognitiveMemory module",
    "Feature-module executors consuming WorkflowExecutors.Abstractions",
    "SB06/SB09",
    "Module-provided executors were missed by default/plugin-only categorization",
    "Descriptor parity, registration, diagnostics, and boundary tests",
    "SB01 repair",
  ],
  [
    "Workbench agent workflow tools",
    "src\\CanDoItAll.Modules.Workbench\\AgentTools\\ProjectStructureAgentRuntimeToolProvider.cs",
    "Workbench module",
    "Consumes isolated workflow/project-structure services",
    "SB12/SB13",
    "Agent tools expose workflow add/create/start/status outside Blazor UI",
    "Workbench agent-tool integration and adoption hardening proof",
    "SB01 repair",
  ],
  [
    "Scheduler workflow input options",
    "SchedulerWorkflowInputSchemaService.cs; SchedulerWorkflowInputOptionService.cs; SchedulerPlannerWorkflowInputOptionProviders.cs",
    "Scheduler/Composition",
    "Consumes workflow template/input contracts",
    "SB10/SB12",
    "Input option consumers can retain UI-owned template coupling",
    "Scheduler input option unit/integration tests",
    "SB01 repair",
  ],
]);

writeRows("Project Targets", "A24:G26", [
  [
    "Feature-module executor sources",
    "Domain modules that provide workflow executors",
    "WorkflowExecutors.Abstractions plus module domain services",
    "MAF/Core executor-contract ownership",
    "SB06",
    "SB09",
    "Descriptor/registration/boundary tests",
  ],
  [
    "Workbench agent workflow tools",
    "Agent tool APIs for workflow add/create/start/status",
    "Workbench services plus isolated workflow runtime/catalog contracts",
    "MAF internals and UI-only template DTOs",
    "SB12",
    "SB13",
    "Agent-tool integration and no-fallback tests",
  ],
  [
    "Scheduler workflow input consumers",
    "Workflow input schema/option providers",
    "Workflows.Templates and workflow input contracts",
    "Blazor module template loader internals",
    "SB10",
    "SB12",
    "Input option compatibility tests",
  ],
]);

writeRows("Executor Categories", "A16:G16", [
  [
    "Feature modules",
    "cognitive-memory.recall, cognitive-memory.probe, cognitive-memory.learning-proposal",
    "src\\CanDoItAll.Modules.CognitiveMemory\\Advanced\\CognitiveMemoryMafIntegration.cs",
    "Owning feature module + WorkflowExecutors.Abstractions",
    "SB06/SB09",
    "Stable ids, automation settings, semantic dependency handling, redacted memory context",
    "Descriptor parity, unavailable dependency, cancellation, and diagnostic tests",
  ],
]);

writeRows("Validation Matrix", "A23:G25", [
  [
    "R07/R08/R14/R17",
    "Feature-module executor source",
    "Cognitive Memory executors consume executor abstractions with stable ids and typed diagnostics",
    "SB06/SB09",
    "Descriptor parity, registration, boundary, and diagnostic tests",
    "N/A",
    "Semantic gate",
  ],
  [
    "R12/R17",
    "Workbench agent workflow tools",
    "Agent workflow add/create/start/status uses isolated services with lease/access behavior",
    "SB12/SB13",
    "Workbench agent-tool integration tests",
    "Large-screen only if UI proof is needed",
    "Execution report",
  ],
  [
    "R10/R12",
    "Scheduler workflow input options",
    "Scheduler consumers use workflow template/input contracts without UI-owned loader coupling",
    "SB10/SB12",
    "Input option and template contract tests",
    "N/A",
    "Semantic gate",
  ],
]);

writeRows("Summary", "A13:H13", [
  [
    "SB01 live repair",
    "3 mapped surfaces",
    "Live rg inventory",
    "SB01",
    "Updated",
    "High",
    "SB02",
    "Added Cognitive Memory executors, Workbench agent workflow tools, and Scheduler workflow input consumers.",
  ],
]);

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(workbookPath);

console.log(`Updated ${workbookPath}`);
