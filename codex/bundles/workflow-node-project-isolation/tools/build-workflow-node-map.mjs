import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const bundleRoot = path.resolve(__dirname, "..");
const inventoryDir = path.join(bundleRoot, "inventories");
const previewDir = path.join(bundleRoot, ".workbook-build", "previews");
const outputPath = path.join(inventoryDir, "workflow-node-project-isolation-map.xlsx");

const workbook = Workbook.create();
const generatedAt = "Prepared 2026-06-29";

const theme = {
  titleFill: "#17324D",
  subtitleFill: "#EAF2F8",
  headerFill: "#246B7A",
  headerFont: "#FFFFFF",
  mutedFill: "#F4F7FA",
  border: "#C9D6DF",
  warningFill: "#FFF4D6",
  highFill: "#FDE2E1",
  okFill: "#E4F5E9",
};

const statusValues = ["Ready", "Planned", "Blocked", "Done", "Deferred", "N/A"];
const priorityValues = ["Critical", "High", "Medium", "Low", "N/A"];
const ownerValues = [
  "SB01",
  "SB02",
  "SB03",
  "SB04",
  "SB05",
  "SB06",
  "SB07",
  "SB08",
  "SB09",
  "SB10",
  "SB11",
  "SB12",
  "SB13",
  "SB14",
  "SB02-SB14",
  "SB05/SB09/SB13",
  "SB01/SB02",
  "SB01/SB05",
  "SB02/SB10",
  "SB03/SB05",
  "SB03/SB06",
  "SB04/SB05",
  "SB04/SB06/SB07/SB09",
  "SB04/SB07",
  "SB05/SB09",
  "SB05/SB07/SB09/SB10/SB11/SB13",
  "SB06/SB07/SB08",
  "SB06/SB08/SB09",
  "SB06-SB14",
  "SB07/SB09",
  "SB07/SB12",
  "SB07-SB14",
  "SB08/SB09",
  "SB08/SB09/SB12",
  "SB10/SB13",
  "SB11/SB13",
  "SB11/SB12/SB13",
  "SB12/SB13",
  "Multiple",
];

function colName(index) {
  let n = index + 1;
  let name = "";

  while (n > 0) {
    const rem = (n - 1) % 26;
    name = String.fromCharCode(65 + rem) + name;
    n = Math.floor((n - rem - 1) / 26);
  }

  return name;
}

function normalizeRows(rows) {
  const width = Math.max(...rows.map((row) => row.length));
  return rows.map((row) => [...row, ...Array(width - row.length).fill(null)]);
}

function addTableSheet({ name, title, note, headers, rows, tableName, widths = [], validations = {} }) {
  const sheet = workbook.worksheets.add(name);
  sheet.showGridLines = false;

  const normalized = normalizeRows([headers, ...rows]);
  const width = normalized[0].length;
  const lastCol = colName(width - 1);

  sheet.getRange(`A1:${lastCol}1`).merge();
  sheet.getRange("A1").values = [[title]];
  sheet.getRange("A1").format = {
    fill: theme.titleFill,
    font: { bold: true, color: "#FFFFFF", size: 15 },
  };

  sheet.getRange(`A2:${lastCol}2`).merge();
  sheet.getRange("A2").values = [[note]];
  sheet.getRange("A2").format = {
    fill: theme.subtitleFill,
    font: { color: "#17324D" },
    wrapText: true,
  };

  sheet.getRangeByIndexes(3, 0, normalized.length, width).values = normalized;
  const tableRange = `A4:${lastCol}${4 + normalized.length - 1}`;
  const table = sheet.tables.add(tableRange, true, tableName);
  table.style = "TableStyleMedium2";
  table.showFilterButton = true;

  sheet.getRange(`A4:${lastCol}4`).format = {
    fill: theme.headerFill,
    font: { bold: true, color: theme.headerFont },
    wrapText: true,
  };

  if (rows.length > 0) {
    const bodyRange = sheet.getRange(`A5:${lastCol}${4 + rows.length}`);
    bodyRange.format = {
      fill: "#FFFFFF",
      font: { color: "#17212B" },
      wrapText: true,
      borders: { preset: "inside", style: "thin", color: theme.border },
    };
  }

  sheet.getRange(`A1:${lastCol}${4 + rows.length}`).format.borders = {
    preset: "outside",
    style: "thin",
    color: theme.border,
  };

  sheet.freezePanes.freezeRows(4);
  sheet.freezePanes.freezeColumns(1);

  headers.forEach((header, index) => {
    const widthPx = widths[index] ?? 190;
    sheet.getRangeByIndexes(0, index, Math.max(6, rows.length + 4), 1).format.columnWidthPx = widthPx;

    if (validations[header] && rows.length > 0) {
      sheet.getRangeByIndexes(4, index, rows.length, 1).dataValidation = {
        rule: { type: "list", values: validations[header] },
      };
    }
  });

  return sheet;
}

const summaryRows = [
  ["Prepared status", "Prepared", "Bundle docs only", "SB01", "Ready", "Low", "Prepared-stage validator", "No production implementation in this phase."],
  ["Subbundle count", 14, "Plan", "Multiple", "Ready", "Medium", "SB01", "Dependency sequence is base-up and checkpoint-gated."],
  ["Hardening checkpoints", 3, "SB05/SB09/SB13", "Multiple", "Ready", "Critical", "SB05", "Forced gates after foundation, executor/plugin, and adoption blocks."],
  ["Plugin consequence owner", "SB08/SB09", "Plugin inventory", "SB08", "Ready", "Critical", "SB08", "Covers manifests, grants, source/trust, side effects, secrets, package loading."],
  ["Failure diagnostics", "R17", "Architecture and error-state inventory", "Multiple", "Ready", "Critical", "SB05/SB09/SB13", "Typed, repairable, redacted diagnostics are required across workflow/executor/plugin/tool failures."],
  ["No copied monoliths", "R18", "Audit and checkpoint requirements", "Multiple", "Ready", "High", "SB05/SB09/SB13", "Large moved classes must be split by responsibility with tests."],
  ["CodeAnalytics snapshot", "snap-20260629143729-e43d210b", "Preparation re-audit", "SB01", "Ready", "Medium", "SB05", "20 source projects, 587 source documents, no blocking snapshot errors."],
  ["Workbook purpose", "Mapping artifact", "User request", "SB01", "Ready", "Medium", "SB01", "Maps source surfaces, target projects, risks, validation, and performance signals."],
];

addTableSheet({
  name: "Summary",
  title: "Workflow Node Project Isolation - Bundle Map",
  note: `${generatedAt}. This workbook maps the prepared initiative bundle; it intentionally does not represent implemented code changes.`,
  headers: ["Metric", "Value", "Evidence", "Owner", "Status", "Priority", "Next Gate", "Notes"],
  rows: summaryRows,
  tableName: "SummaryTable",
  widths: [190, 220, 230, 110, 110, 110, 170, 430],
  validations: { Owner: ownerValues, Status: statusValues, Priority: priorityValues },
});

const sourceRows = [
  ["Workflow models", String.raw`src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`, "Models", "Workflows.Abstractions or retained model package", "SB02", "Serialized fields must remain compatible", "Serialization and builder tests", "Ready"],
  ["Input parameter models", String.raw`src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowInputParameterModels.cs`, "Models", "Workflows.Abstractions or retained model package", "SB02", "Template/UI input compatibility", "Input parameter mapping tests", "Ready"],
  ["Workflow contracts", String.raw`src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs`, "Core", "Workflows.Abstractions", "SB02", "Contract movement can create circular references", "Boundary tests", "Ready"],
  ["Executor contracts", String.raw`src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`, "Core", "WorkflowExecutors.Abstractions", "SB06", "Executor ids/descriptors must stay stable", "Descriptor parity tests", "Ready"],
  ["Validator", String.raw`src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs`, "Core", "Workflows.Core", "SB03", "Invalid graphs must fail explicitly", "Positive/negative validation tests", "Ready"],
  ["Runtime manager", String.raw`src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowRuntimeManager.cs`, "Core", "Workflows.Runtime", "SB04", "Run lifecycle and events can regress", "Lifecycle and event tests", "Ready"],
  ["Artifact stores", String.raw`src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowArtifactContentStores.cs`, "Core", "Workflows.Runtime/Persistence", "SB04", "Hidden in-memory fallback risk", "Store failure tests", "Ready"],
  ["Built-in executor registration", String.raw`src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorServiceCollectionExtensions.cs`, "MAF", "WorkflowExecutors.Standard.* registrations", "SB07", "MAF must not remain executor owner", "Registration and no-fallback tests", "Ready"],
  ["Built-in descriptors", String.raw`src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorDescriptors.cs`, "MAF", "WorkflowExecutors.Core/Standard.*", "SB06/SB07", "Descriptor schema parity", "Descriptor parity tests", "Ready"],
  ["Control executors", String.raw`src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\ControlWorkflowExecutors.cs`, "MAF", "WorkflowExecutors.Standard.Control", "SB07", "Branch/delay/loop semantics", "Per-executor tests", "Ready"],
  ["Transform executors", String.raw`JsonTransformWorkflowExecutor.cs; MarkdownRenderWorkflowExecutor.cs`, "MAF", "WorkflowExecutors.Standard.Transforms", "SB07", "JSON/Markdown output compatibility", "Golden-output tests", "Ready"],
  ["Workspace and source executors", String.raw`WorkspaceFileWorkflowExecutor.cs; SourceIngestionWorkflowExecutor.cs`, "MAF", "WorkflowExecutors.Standard.Workspace", "SB07", "File/content side effects and caps", "Side-effect tests", "Ready"],
  ["Network executor", String.raw`HttpFetchWorkflowExecutor.cs`, "MAF", "WorkflowExecutors.Standard.Network", "SB07", "Network limits, timeout, and response handling", "Policy, timeout, and deterministic preview tests", "Ready"],
  ["Document executor", String.raw`SpreadsheetWorkflowExecutor.cs`, "MAF", "WorkflowExecutors.Standard.Documents", "SB07", "Spreadsheet/document dependencies and file writes", "Document IO and failure tests", "Ready"],
  ["Media executor", String.raw`ImageGenerationWorkflowExecutor.cs`, "MAF", "WorkflowExecutors.Standard.Media", "SB07", "Image/media provider limits and artifacts", "Provider, artifact, and deterministic preview tests", "Ready"],
  ["Project structure executor", String.raw`ProjectStructureWorkflowExecutor.cs`, "MAF", "WorkflowExecutors.Standard.ProjectStructure", "SB07", "Workbench integration", "Workbench executor tests", "Ready"],
  ["Template loader", String.raw`src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowTemplatePackLoader.cs`, "Blazor module", "Workflows.Templates", "SB10", "UI-owned template parsing must move", "All-template load tests", "Ready"],
  ["MAF compiler/backend", String.raw`src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\MafWorkflowCompiler.cs; MafInProcessWorkflowExecutionBackend.cs`, "MAF", "Workflows.MafAdapter", "SB11", "MAF must become adapter only", "Adapter integration tests", "Ready"],
  ["Workflow API", String.raw`src\CanDoItAll.Web\Api\WorkflowsApi.cs`, "Web", "Consumes isolated workflow services", "SB12", "API contract compatibility", "API/integration tests", "Ready"],
  ["Workflow UI/editor", String.raw`src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage*; WorkflowCanvasEditor*`, "Blazor module", "Consumes isolated services", "SB12", "Visible behavior/canvas state", "Component and browser tests", "Ready"],
  ["Workbench workflow nodes", String.raw`src\CanDoItAll.Modules.Workbench\ProjectStructure`, "Workbench module", "Consumes isolated project-structure workflow services", "SB12", "Project-structure workflow path", "Workbench and browser tests", "Ready"],
  ["Plugin descriptor source", String.raw`src\CanDoItAll.Modules.Plugins\Catalog\PluginWorkflowExecutorDescriptorSource.cs`, "Plugins module", "WorkflowExecutors.Plugins", "SB08", "Trust/source/grants display", "Plugin descriptor tests", "Ready"],
  ["Plugin package loading", String.raw`src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs`, "Plugins module", "WorkflowExecutors.Plugins", "SB08", "Installed package compatibility", "Package loading tests", "Ready"],
  ["Failure diagnostics", String.raw`WorkflowFailureDisplayFormatter.cs; WorkflowExecutorContracts.cs; WorkflowExecutorObservability.cs`, "Core/MAF", "Workflows.Abstractions/Core + WorkflowExecutors.Core", "SB02-SB14", "Generic errors and string parsing can lose repair context", "Typed diagnostic, redaction, and no-generic-error tests", "Ready"],
];

addTableSheet({
  name: "Source Map",
  title: "Source Surface To Target Owner",
  note: "Every known workflow/executor/plugin/template/UI surface from preparation is assigned to an owner and validation path.",
  headers: ["Area", "Current Source", "Current Owner", "Target Owner", "Owning SB", "Risk", "Validation", "Status"],
  rows: sourceRows,
  tableName: "SourceMapTable",
  widths: [190, 360, 170, 260, 110, 280, 260, 110],
  validations: { "Owning SB": ownerValues, Status: statusValues },
});

const projectRows = [
  ["Workflows.Abstractions", "Stable workflow contracts and serialized shapes", "Models/core abstractions only", "MAF, UI, plugins, persistence implementations", "SB02", "SB02", "Boundary and serialization tests"],
  ["Workflows.Builder", "Workflow graph builders, factories, and fixtures", "Workflows.Abstractions", "MAF, UI, plugin implementations", "SB02", "SB10/SB12", "Builder output tests"],
  ["Workflows.Core", "Validation, catalog, routing, preview, payload policy, failure formatting, process bridge", "Workflows.Abstractions/Builder", "MAF and UI modules", "SB03", "SB05", "Validator/catalog/routing tests"],
  ["Workflows.Runtime", "Run lifecycle, checkpoint/artifact contracts, event payloads, backend catalog, external requests", "Workflows.Abstractions/Core; executor abstractions", "Concrete MAF/default/plugin executors", "SB04", "SB05/SB11", "Lifecycle and store tests"],
  ["Workflows.Persistence", "Persistence-backed workflow stores", "Workflows.Runtime; persistence infra", "MAF adapter and UI", "SB04", "SB05", "Store integration tests"],
  ["WorkflowExecutors.Abstractions", "Executor contracts, descriptors, catalog/invoker contracts, policies", "Workflow/workspace abstractions", "MAF, UI, plugin implementations", "SB06", "SB07/SB08", "Descriptor and boundary tests"],
  ["WorkflowExecutors.Core", "Shared descriptor, settings, JSON, redaction, policy helpers", "Executor abstractions", "Default/plugin implementation details unless intentionally shared", "SB06", "SB09", "Redaction/policy/helper tests"],
  ["WorkflowExecutors.Standard.Control", "Control-flow default executors", "Executor abstractions/core; workflow runtime as needed", "MAF", "SB07", "SB09", "Control executor tests"],
  ["WorkflowExecutors.Standard.Transforms", "JSON and Markdown transform executors", "Executor abstractions/core", "MAF", "SB07", "SB09", "Golden output tests"],
  ["WorkflowExecutors.Standard.Workspace", "Workspace file and source ingestion executors", "Executor abstractions/core; workspace services", "MAF", "SB07", "SB09", "File/content side-effect tests"],
  ["WorkflowExecutors.Standard.Network", "HTTP and network data executors", "Executor abstractions/core; external service abstractions", "MAF", "SB07", "SB09", "Network policy and timeout tests"],
  ["WorkflowExecutors.Standard.Documents", "Spreadsheet and document executors", "Executor abstractions/core; document services", "MAF", "SB07", "SB09", "Document IO and compatibility tests"],
  ["WorkflowExecutors.Standard.Media", "Image generation and media executors", "Executor abstractions/core; provider abstractions", "MAF", "SB07", "SB09", "Provider, artifact, and policy tests"],
  ["WorkflowExecutors.Standard.ProjectStructure", "Project-structure workflow executor implementation", "Executor abstractions/core; project-structure services", "MAF/UI internals", "SB07", "SB12", "Workbench executor tests"],
  ["WorkflowExecutors.Plugins", "Plugin descriptor projection and runtime executor adapter", "Executor abstractions/core; plugin abstractions", "MAF", "SB08", "SB09/SB12", "Manifest/package/grant tests"],
  ["Workflows.Templates", "Template pack loading and descriptor-aware materialization", "Workflows.Builder/Core; executor abstractions", "Blazor UI module and MAF", "SB10", "SB11/SB12", "All-template tests"],
  ["Workflows.MafAdapter", "MAF compiler/backend/LLM/event adapter", "Workflows runtime/templates; executor catalog", "Workflow abstractions/core as reverse dependency", "SB11", "SB12", "Adapter integration tests"],
  ["Workflows.Hosting", "Composition extension for isolated workflow/executor stack", "All isolated implementation projects", "Feature logic ownership", "SB11", "SB12/SB13", "Service composition tests"],
  ["Workflow failure diagnostics", "Typed diagnostic envelopes, redaction, retryability, and repair hints", "Workflow and executor abstractions", "UI-only string parsing and raw exception display", "SB02", "SB05/SB09/SB13", "No-generic-error and redaction tests"],
];

addTableSheet({
  name: "Project Targets",
  title: "Target Project Graph And Dependency Rules",
  note: "Project names are architecture targets prepared for implementation; implementation must validate exact names against solution conventions.",
  headers: ["Target Project", "Role", "Allowed Dependencies", "Must Not Depend On", "Created In", "Adopted In", "Validation"],
  rows: projectRows,
  tableName: "ProjectTargetsTable",
  widths: [270, 380, 330, 320, 110, 120, 250],
  validations: { "Created In": ownerValues, "Adopted In": ownerValues },
});

const subbundleRows = [
  ["SB01", "Boundary inventory and project graph", "Confirm all source surfaces and target ownership", "Foundation", "Critical", "None", "SB02", "Inventory, workbook render"],
  ["SB02", "Workflow abstractions and builders", "Create strongly typed workflow contracts/builders", "Foundation", "Critical", "SB01", "SB03/SB04", "Build, builder, serialization, boundary tests"],
  ["SB03", "Workflow core services extraction", "Move validator/catalog/routing/policy services", "Foundation", "Critical", "SB02", "SB05", "Core parity and negative tests"],
  ["SB04", "Runtime and store abstractions", "Move lifecycle/store/artifact/event boundaries", "Foundation", "Critical", "SB02/SB03", "SB05", "Lifecycle/store/backend tests"],
  ["SB05", "Foundation hardening checkpoint", "Block executor work until base is clean", "Hardening", "Critical", "SB03/SB04", "SB06", "Architecture, diagnostics, performance scans"],
  ["SB06", "Executor abstractions and helpers", "Create executor-owned contracts/core helpers", "Executor", "Critical", "SB05", "SB07/SB08", "Descriptor, redaction, policy tests"],
  ["SB07", "Default executor categories", "Move built-ins into logical category projects", "Executor", "Critical", "SB06", "SB09", "Per-category parity tests"],
  ["SB08", "Plugin executor adapters", "Preserve plugin executors through explicit boundary", "Plugin", "Critical", "SB06", "SB09", "Manifest/package/grant/security tests"],
  ["SB09", "Executor hardening checkpoint", "Block templates until executor/plugin proof passes", "Hardening", "Critical", "SB07/SB08", "SB10", "Combined executor/plugin scans"],
  ["SB10", "Templates and descriptor loading", "Move YAML/template materialization out of UI", "Template", "Critical", "SB09", "SB11", "All-template and negative tests"],
  ["SB11", "MAF adapter isolation", "Make MAF an adapter, not owner", "Adapter", "Critical", "SB10", "SB12", "Adapter and no-reverse-dependency tests"],
  ["SB12", "API/UI/Workbench adoption", "Adopt isolated services in user-facing surfaces", "Adoption", "Critical", "SB11", "SB13", "API, component, browser proof"],
  ["SB13", "Adoption hardening checkpoint", "Prove live adoption and no fallback paths", "Hardening", "Critical", "SB12", "SB14", "No-fallback, browser, performance scans"],
  ["SB14", "Regression cleanup and docs", "Final cleanup, docs, completed validation", "Closure", "Critical", "SB13", "Complete", "Final regression and validator"],
];

addTableSheet({
  name: "Subbundles",
  title: "Dependency-Aware Subbundle Sequence",
  note: "The sequence intentionally builds from contracts upward, with forced hardening gates before dependent adoption.",
  headers: ["Id", "Name", "Objective", "Type", "Priority", "Prerequisites", "Blocks", "Validation"],
  rows: subbundleRows,
  tableName: "SubbundlesTable",
  widths: [90, 260, 380, 130, 110, 180, 150, 330],
  validations: { Priority: priorityValues },
});

const executorRows = [
  ["Control", "branch, delay, planned/placeholder control nodes", "ControlWorkflowExecutors.cs; PlannedWorkflowExecutor.cs", "WorkflowExecutors.Standard.Control", "SB07", "Executor ids, branch routing, cancellation, deterministic behavior", "Per-executor positive/negative diagnostics tests"],
  ["Transforms", "json.transform, markdown.render", "JsonTransformWorkflowExecutor.cs; MarkdownRenderWorkflowExecutor.cs", "WorkflowExecutors.Standard.Transforms", "SB07", "Settings schema, output shape, encoding, generated regex behavior", "Golden output, invalid settings, and JSON path diagnostics tests"],
  ["Workspace", "workspace.file, source ingestion", "WorkspaceFileWorkflowExecutor.cs; SourceIngestionWorkflowExecutor.cs", "WorkflowExecutors.Standard.Workspace", "SB07", "File access policy, output caps, content handling, helper split", "Side-effect, path, cap, and failure diagnostics tests"],
  ["Network", "http.fetch", "HttpFetchWorkflowExecutor.cs", "WorkflowExecutors.Standard.Network", "SB07", "Network side effects, timeout, limits, deterministic preview", "Policy, preview, timeout, and provider failure tests"],
  ["Documents", "spreadsheet", "SpreadsheetWorkflowExecutor.cs", "WorkflowExecutors.Standard.Documents", "SB07", "Spreadsheet/document dependencies and file writes", "Document IO, invalid range, and artifact failure tests"],
  ["Media", "image generation", "ImageGenerationWorkflowExecutor.cs", "WorkflowExecutors.Standard.Media", "SB07", "Media provider side effects, limits, deterministic preview", "Provider, preview, timeout, and artifact tests"],
  ["Project structure", "project-structure workflow node execution", "ProjectStructureWorkflowExecutor.cs; Workbench ProjectStructure services", "WorkflowExecutors.Standard.ProjectStructure", "SB07/SB12", "Workbench settings, project graph compatibility, helper split", "Workbench service/browser and gateway failure tests"],
  ["Plugin Docker", "Docker workflow executor", String.raw`src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs`, "WorkflowExecutors.Plugins + plugin project", "SB08", "Host-tool grants, approval, command masking, output caps", "Bundled plugin grant/approval/host-command failure tests"],
  ["Plugin Gmail", "Gmail workflow executor", String.raw`src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs`, "WorkflowExecutors.Plugins + plugin project", "SB08", "OAuth, secrets, external read/write, idempotency receipts", "Bundled plugin secret/receipt/provider failure tests"],
  ["Plugin Office365", "Office365 workflow executor", String.raw`src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs`, "WorkflowExecutors.Plugins + plugin project", "SB08", "Graph read/write, processed marker receipts, deterministic preview", "Bundled plugin Graph/provider failure tests"],
  ["Runtime package executors", "Installed plugin assemblies assignable to workflow executor contracts", "PluginPackageServices.cs runtime scan", "WorkflowExecutors.Plugins", "SB08", "Manifest compatibility, runtime discovery, package/type diagnostics", "Package loading, dependency, and activation failure tests"],
];

addTableSheet({
  name: "Executor Categories",
  title: "Executor Category Ownership",
  note: "Default executors move into category projects; plugin executors remain plugin-owned but adapt through executor/plugin boundary services.",
  headers: ["Category", "Executors", "Current Source", "Target Project", "Owner", "Compatibility Invariants", "Tests"],
  rows: executorRows,
  tableName: "ExecutorCategoriesTable",
  widths: [190, 290, 390, 300, 120, 420, 320],
  validations: { Owner: ownerValues },
});

const pluginRows = [
  ["Plugin manifest contracts", String.raw`CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs`, "WorkflowExecutor capability and descriptors must remain loadable by installed packages", "SB08", "Backward-compatible manifest tests", "High", "Do not break schema without migration"],
  ["Plugin execution contracts", String.raw`CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs`, "IPluginWorkflowExecutor bridge must stay source-compatible", "SB08", "Compile and adapter tests", "High", "Strongly typed context; no service locator fallback"],
  ["Descriptor projection", "PluginWorkflowExecutorDescriptorSource.cs", "Source/trust/grant/permission metadata drives UI and runtime policy", "SB08", "Descriptor projection parity tests", "Critical", "Trust/source cannot be collapsed to generic built-in"],
  ["Runtime package loading", "PluginPackageServices.cs", "Installed assemblies scan for executor types and register runtime package executors", "SB08", "Package loading integration tests", "Critical", "No MAF-only adapter fallback"],
  ["Docker bundled executor", "DockerWorkflowExecutors.cs", "Host command side effects require grants, approval, output caps, deterministic preview", "SB08", "Grant/approval/preview tests", "Critical", "Mask command-sensitive data"],
  ["Gmail bundled executor", "GmailWorkflowExecutor.cs", "OAuth and external read/write require secrets masking and idempotency receipts", "SB08", "OAuth/receipt/deterministic preview tests", "Critical", "No token/email leakage"],
  ["Office365 bundled executor", "Office365WorkflowExecutor.cs", "Graph write/read and processed marker receipts must survive adapter move", "SB08", "Graph/receipt tests", "Critical", "No token/content leakage"],
  ["Plugin audit sink", "PluginsModuleServiceCollectionExtensions.cs", "Executor registration and execution events must remain auditable", "SB08/SB09", "Audit event tests", "High", "Logs must include actionable state only"],
  ["Plugin UI display", "WorkflowExecutorDisplayAdapter.cs", "Plugin source/trust metadata remains visible after service adoption", "SB12/SB13", "Component/browser display tests", "Medium", "UI must not infer plugin state from strings"],
  ["Plugin failure diagnostics", "Runtime package adapter; bundled plugin executors", "Plugin/package/type/operation failure context must survive adapter boundaries", "SB08/SB09/SB12", "No-generic-error and redaction tests", "Critical", "Repairable user message plus secure correlation id"],
  ["Plugin package activation", "PluginPackageServices.cs", "Package load and DI activation errors need package/type/dependency context", "SB08/SB09", "Package load negative tests", "Critical", "No raw path/secret leakage"],
];

addTableSheet({
  name: "Plugin Consequences",
  title: "Plugin Workflow Executor Consequence Map",
  note: "Plugin executor isolation is treated as critical because plugins are a major source of executor implementations and side effects.",
  headers: ["Surface", "Current Source", "Consequence", "Owner", "Compatibility Proof", "Priority", "Security/Perf Note"],
  rows: pluginRows,
  tableName: "PluginConsequencesTable",
  widths: [220, 330, 440, 120, 300, 110, 360],
  validations: { Owner: ownerValues, Priority: priorityValues },
});

const validationRows = [
  ["R01", "Preparation-only scope", "No production source implementation", "SB01-SB14", "Git diff/bundle validator", "N/A", "Prepared validator"],
  ["R02", "All surfaces inventoried", "Workflow/executor/plugin/template/API/UI/test rows mapped", "SB01", "Inventory review", "N/A", "Workbook previews"],
  ["R03", "Base-up project graph", "Allowed dependencies and target projects documented", "SB01/SB02", "Architecture tests later", "N/A", "Project Targets sheet"],
  ["R04", "Builders/factories", "Graph construction not duplicated", "SB02/SB10", "Builder/template tests", "N/A", "Semantic gate"],
  ["R05", "Core services extraction", "Validation/catalog/routing parity", "SB03/SB05", "Unit/integration tests", "N/A", "Semantic gate"],
  ["R06", "Runtime/store extraction", "Lifecycle/store/artifact/event parity", "SB04/SB05", "Lifecycle/store tests", "N/A", "Semantic gate"],
  ["R07", "Executor abstractions/helpers", "Contracts/helpers in executor-owned projects", "SB06/SB09", "Descriptor/redaction tests", "N/A", "Semantic gate"],
  ["R08", "Default executor categories", "Default executors split by logic", "SB07/SB09", "Per-category parity tests", "N/A", "Semantic gate"],
  ["R09", "Plugin compatibility", "Manifest/package/grant/secret/side-effect proof", "SB08/SB09", "Plugin integration tests", "N/A", "Semantic gate"],
  ["R10", "Template loading isolation", "Templates load outside UI module", "SB10/SB13", "All-template tests", "N/A", "Semantic gate"],
  ["R11", "MAF adapter only", "MAF compiler/backend behind adapter", "SB11/SB13", "Adapter/no-fallback tests", "N/A", "Semantic gate"],
  ["R12", "API/UI/Workbench adoption", "User-facing surfaces consume isolated services", "SB12/SB13", "API/component tests", "Browser required", "Screenshots and DOM logs"],
  ["R13", "Hardening checkpoints", "SB05/SB09/SB13 block dependent work", "SB05/SB09/SB13", "Checkpoint reports", "Focused browser in SB13", "Execution report"],
  ["R14", "Compatibility invariants", "Ids/templates/events/receipts/test mode stable", "SB07-SB14", "Regression tests", "Browser in SB12/SB14", "Anti-stub audit"],
  ["R15", "Artifact-backed proof", "Proof manifests and semantic invariants", "SB02-SB14", "Proof manifest review", "As applicable", "Semantic Adequacy Gate"],
  ["R16", "XLSX mapping", "Workbook exists and renders", "SB01", "Inspect/render/export", "N/A", "This workbook"],
  ["R17", "Actionable failure diagnostics", "Typed diagnostic envelope, repair hints, redaction, retryability, and no generic errors", "SB02-SB14", "Negative runtime/executor/plugin/tool tests", "Browser in SB12/SB14", "Error States sheet"],
  ["R18", "No copied monoliths", "Large moved files split by responsibility with helper tests", "SB05/SB07/SB09/SB10/SB11/SB13", "File-size/responsibility scans", "N/A", "Hardening reports"],
];

addTableSheet({
  name: "Validation Matrix",
  title: "Requirement To Validation Mapping",
  note: "Validation scopes are planned for implementation; browser proof is intentionally delayed until UI/adoption subbundles.",
  headers: ["Requirement", "Validation Target", "Acceptance Evidence", "Owner", "Build/Test Proof", "Browser Proof", "Proof Artifact"],
  rows: validationRows,
  tableName: "ValidationMatrixTable",
  widths: [120, 320, 390, 140, 300, 210, 280],
  validations: { Owner: ownerValues },
});

const errorStateRows = [
  ["Missing executor id", "Validation/runtime", "Node id and repair hint to choose an executor", "SB03/SB06", "Validator and invoker negative tests", "Critical"],
  ["Executor unavailable", "Descriptor catalog/plugin grants", "Unavailable descriptor reason, executor id, source/plugin context", "SB06/SB08/SB09", "Descriptor unavailable tests", "Critical"],
  ["Invalid settings", "Default and plugin executors", "Executor id, node id, setting path/name, JSON summary, repair hint", "SB06/SB07/SB08", "Invalid settings tests per category", "Critical"],
  ["Timeout/cancellation", "Invoker/runtime/executors", "Attempt, timeout seconds, cancellation classification, retryability", "SB04/SB06/SB07/SB09", "Timeout and cancellation tests", "High"],
  ["Payload/artifact/store failure", "Runtime and executor output", "Payload cap, artifact/checkpoint/store operation, correlation id", "SB04/SB07/SB09", "Payload cap and store failure tests", "High"],
  ["Workspace path denied", "Workspace/file/source ingestion", "Safe path scope, denied operation, repair hint", "SB04/SB07", "Path traversal and unauthorized path tests", "High"],
  ["Template failure", "Template loader/materializer", "Template file/key/workflow key/YAML path/node/executor context", "SB10/SB13", "Malformed template tests", "High"],
  ["MAF compile/backend failure", "MAF adapter", "Backend kind, compile stage, node binding/event context, correlation id", "SB11/SB13", "Compile/backend failure tests", "High"],
  ["External tool/MCP failure", "MAF adapter/tool invoker", "Provider/server/tool/operation, safe status or exit code, retryability", "SB11/SB12/SB13", "Tool/MCP negative tests and UI proof", "Critical"],
  ["Plugin package load failure", "Runtime package loading", "Package id, plugin id, type/dependency when known, repair hint", "SB08/SB09", "Package load negative tests", "Critical"],
  ["Plugin activation failure", "Runtime package DI", "Plugin id, package id, executor type, missing service/dependency when safe", "SB08/SB09", "DI activation negative tests", "Critical"],
  ["Plugin grant/OAuth/secret failure", "Plugin descriptor and execution", "Grant/connection/secret state with masked identifiers and repair hint", "SB08/SB09/SB12", "Grant/OAuth/secret masking tests", "Critical"],
  ["Plugin provider failure", "Docker/Gmail/Office365", "Operation/provider status, receipt state, retryability, redacted provider detail", "SB08/SB09", "Host command and provider failure tests", "Critical"],
  ["Unknown exception", "All boundaries", "Known ids, source kind, sanitized exception type/message, secure correlation id", "SB06-SB14", "No-generic-error and redaction audit", "High"],
];

addTableSheet({
  name: "Error States",
  title: "Failure And Error-State Coverage",
  note: "Every failure category must remain actionable after project isolation; generic errors are not acceptable closure proof.",
  headers: ["Failure State", "Surface", "Required Context", "Owner", "Required Proof", "Priority"],
  rows: errorStateRows,
  tableName: "ErrorStatesTable",
  widths: [230, 260, 470, 170, 330, 110],
  validations: { Owner: ownerValues, Priority: priorityValues },
});

const performanceRows = [
  ["CodeAnalytics snapshot", "snap-20260629143729-e43d210b", "20 source projects and 587 source documents scanned with no blocking snapshot errors", "Inventory confidence", "SB01/SB05", "Keep snapshot id in proof"],
  ["Large/complex workflow files", "WorkflowTemplatePackLoader.cs; MafInProcessWorkflowExecutionBackend.cs; ProjectStructureWorkflowExecutor.cs; SourceIngestionWorkflowExecutor.cs", "Roughly 680-770 line files observed in current workflow/executor/template/backend surfaces", "Maintainability and testability risk", "SB07/SB10/SB11/SB13", "Split by responsibility, add focused tests"],
  ["LINQ chains", "Broad workflow/executor/plugin scoped scan", "3671 broad heuristic candidates", "Repeated descriptor/template/plugin paths may allocate", "SB05/SB09/SB13", "Profile/scan before optimizing; avoid speculative rewrite"],
  ["Repeated JsonSerializerOptions", "Broad workflow/executor scoped scan", "25 heuristic hits", "Allocation and inconsistency risk", "SB06/SB09", "Centralize options where stable and tested"],
  ["Regex usage", "DockerHostToolService; WorkflowExecutorObservability; MarkdownRenderWorkflowExecutor", "6 compiled regex and 1 GeneratedRegex finding", "Startup/perf and trimming review", "SB09", "Prefer generated regex where appropriate; verify not speculative"],
  ["Params signatures", "Backend/event normalizer/project-structure executor", "4 hits", "Potential allocation in hot loops", "SB09/SB13", "Review only if path is hot or repeated"],
  ["Collections", "new List/new Dictionary scoped scan", "26 List and 20 Dictionary constructions", "Mostly normal; watch repeated descriptor aggregation", "SB05/SB09", "Use capacity/frozen collections only with evidence"],
  ["Async/cancellation", "Scoped scan", "No async void findings", "Cancellation still must be preserved across moves", "SB04/SB07/SB08", "Cancellation tests for runtime/executors"],
  ["String comparisons", "Scoped scan", "No missing StringComparison heuristic hits", "Keep explicit comparisons during moves", "SB05/SB09", "Code review and tests"],
];

addTableSheet({
  name: "Performance Signals",
  title: "Performance And Maintainability Signals",
  note: "These are preparation signals from dotnet performance skills; implementation must validate before optimizing.",
  headers: ["Source", "Signal", "Finding", "Risk", "Owner", "Planned Hardening"],
  rows: performanceRows,
  tableName: "PerformanceSignalsTable",
  widths: [220, 330, 430, 300, 140, 390],
  validations: { Owner: ownerValues },
});

await fs.mkdir(inventoryDir, { recursive: true });
await fs.mkdir(previewDir, { recursive: true });

const workbookInspect = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 2500,
  tableMaxRows: 1,
  tableMaxCols: 4,
});
console.log(workbookInspect.ndjson);

const formulaErrors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
  summary: "final formula error scan",
});
console.log(formulaErrors.ndjson);

for (const sheetName of [
  "Summary",
  "Source Map",
  "Project Targets",
  "Subbundles",
  "Executor Categories",
  "Plugin Consequences",
  "Validation Matrix",
  "Error States",
  "Performance Signals",
]) {
  const preview = await workbook.render({
    sheetName,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    path.join(previewDir, `${sheetName.replaceAll(" ", "-").toLowerCase()}.png`),
    new Uint8Array(await preview.arrayBuffer()),
  );
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
await fs.rm(`${outputPath}.inspect.ndjson`, { force: true });
console.log(`Saved ${outputPath}`);
