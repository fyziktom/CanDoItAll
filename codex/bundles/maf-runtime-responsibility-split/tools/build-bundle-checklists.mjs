import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const toolsDir = path.dirname(__filename);
const bundleDir = path.resolve(toolsDir, "..");
const previewDir = path.join(bundleDir, "evidence", "workbook-previews");
const outputPath = path.join(bundleDir, "bundle-checklists.xlsx");
const artifactToolModule = process.env.CODEX_ARTIFACT_TOOL_MODULE ?? "@oai/artifact-tool";
const { SpreadsheetFile, Workbook } = await import(artifactToolModule);

const workbook = Workbook.create();

const colors = {
  navy: "#16324F",
  teal: "#1F7A72",
  blue: "#2563EB",
  amber: "#F59E0B",
  red: "#B91C1C",
  green: "#15803D",
  grayFill: "#F3F6F8",
  line: "#D7DEE8",
  white: "#FFFFFF",
};

function addSheet(name) {
  const sheet = workbook.worksheets.add(name);
  sheet.showGridLines = false;
  return sheet;
}

function writeTitle(sheet, title, subtitle, lastColumn) {
  const titleRange = sheet.getRange(`A1:${lastColumn}1`);
  titleRange.merge();
  titleRange.values = [[title]];
  titleRange.format = {
    fill: colors.navy,
    font: { bold: true, color: colors.white, size: 16 },
  };
  titleRange.format.rowHeight = 30;

  const subtitleRange = sheet.getRange(`A2:${lastColumn}2`);
  subtitleRange.merge();
  subtitleRange.values = [[subtitle]];
  subtitleRange.format = {
    fill: "#E7F2F1",
    font: { color: "#173B3A", size: 10 },
  };
  subtitleRange.format.wrapText = true;
  subtitleRange.format.rowHeight = 34;
}

function writeTable(sheet, startCell, headers, rows, tableName) {
  void tableName;
  const start = parseCell(startCell);
  const range = sheet.getRangeByIndexes(start.row, start.col, rows.length + 1, headers.length);
  range.values = [headers, ...rows];
  sheet.getRangeByIndexes(start.row, start.col, 1, headers.length).format = {
    fill: colors.teal,
    font: { bold: true, color: colors.white },
  };
  range.format.borders = { preset: "inside", style: "thin", color: colors.line };
  range.format.wrapText = true;
  sheet.getRangeByIndexes(start.row, start.col, 1, headers.length).format.rowHeight = 24;
  sheet.getRangeByIndexes(start.row + 1, start.col, rows.length, headers.length).format.rowHeight = 38;
  return range;
}

function parseCell(cell) {
  const match = /^([A-Z]+)(\d+)$/u.exec(cell);
  if (!match) {
    throw new Error(`Invalid cell address: ${cell}`);
  }

  let col = 0;
  for (const char of match[1]) {
    col = col * 26 + (char.charCodeAt(0) - 64);
  }

  return { row: Number(match[2]) - 1, col: col - 1 };
}

function setColumnWidths(sheet, widths) {
  widths.forEach((width, index) => {
    const column = String.fromCharCode("A".charCodeAt(0) + index);
    sheet.getRange(`${column}1:${column}120`).format.columnWidth = width;
  });
}

const requirements = [
  ["R01", "Prepare bundle only and preserve raw request.", "SB01", "No production source changes; prepared validator passes.", "Planned"],
  ["R02", "Split MafAgentRuntime by responsibility, not only by partial classes.", "SB07", "Runtime delegates to named collaborators; static scan passes.", "Planned"],
  ["R03", "Isolate finalizers as driver, strategy, or focused helpers.", "SB06", "Finalizer semantic proof passes with negative and positive cases.", "Planned"],
  ["R04", "Move stable hashing to a whole-project helper when dependency direction permits.", "SB02", "Shared helper tests and dependency scan pass.", "Planned"],
  ["R05", "Move argument value formatting to a MAF-specific helper.", "SB02", "Formatter tests cover primitives, JSON, collections, truncation, hashes.", "Planned"],
  ["R06", "Extract session behavior into a SessionBuilder or equivalent.", "SB03", "Session builder tests and recovery integration tests pass.", "Planned"],
  ["R07", "Extract model parameter behavior into a builder.", "SB04", "Temperature, retry, reasoning effort, and model resolution tests pass.", "Planned"],
  ["R08", "Extract context manifest behavior into a ContextManifestBuilder or equivalent.", "SB05", "Manifest source, total, and token-estimate tests pass.", "Planned"],
  ["R09", "Keep strongly typed boundaries and avoid new magic strings.", "SB01-SB07", "Source review and tests show typed options/constants.", "Planned"],
  ["R10", "Preserve finalizer, session, provider, tool, and context behavior.", "SB03-SB08", "Focused unit, integration, and UI proof pass.", "Planned"],
  ["R11", "Create detailed xlsx checklists.", "SB01", "Workbook exists, renders, and is referenced by bundle.", "Planned"],
  ["R12", "Include UI testing after changes.", "SB08", "Playwright routes, assertions, screenshots, and analytics review recorded.", "Planned"],
];

const subbundles = [
  ["SB01", "Inventory And Refactor Boundaries", "Yes", "None", "Inventory, thresholds, characterization gaps", "proof/SB01/manifest.md"],
  ["SB02", "Shared Helpers And Argument Formatting", "Yes", "SB01", "Hash and formatter helpers extracted and tested", "proof/SB02/manifest.md"],
  ["SB03", "Session Builder Extraction", "Yes", "SB01, SB02", "Session behavior delegated and tested", "proof/SB03/manifest.md"],
  ["SB04", "Model Parameters Builder Extraction", "Yes", "SB01, SB02", "Model options delegated and tested", "proof/SB04/manifest.md"],
  ["SB05", "Context Manifest Builder Extraction", "Yes", "SB01, SB02", "Manifest builder delegated and tested", "proof/SB05/manifest.md"],
  ["SB06", "Finalizer Driver Isolation", "Yes", "SB01, SB02, SB03", "Finalizer semantic proof passes", "proof/SB06/manifest.md"],
  ["SB07", "Runtime Orchestration Slimming", "Yes", "SB01-SB06", "MafAgentRuntime shrinks and no catch-all helpers exist", "proof/SB07/manifest.md"],
  ["SB08", "Regression And UI Proof", "No", "SB01-SB07", "Builds, tests, Playwright, screenshots, raw closure", "proof/SB08/manifest.md"],
];

const checklist = [
  ["SB01", "C01", "Inventory", "Rerun line counts for all MAF runtime .cs files.", "Implementer", "Planned", "Transcript", "proof/SB01/transcripts/line-counts.txt"],
  ["SB01", "C02", "Inventory", "Map MafAgentRuntime method clusters to target collaborators.", "Implementer", "Planned", "Source assertions", "proof/SB01/source-assertions.md"],
  ["SB01", "C03", "Gate", "Set max line threshold for MafAgentRuntime.cs and new collaborators.", "Architect", "Planned", "Execution report", "reviews/01-execution-report.md"],
  ["SB02", "C04", "Hash", "Characterize current ComputeStableHash output including casing and length.", "Implementer", "Planned", "Unit tests", "proof/SB02/transcripts/failing-first-hash.txt"],
  ["SB02", "C05", "Hash", "Place shared helper in approved foundation location only after dependency scan.", "Implementer", "Planned", "Dependency scan", "proof/SB02/transcripts/dependency-direction.txt"],
  ["SB02", "C06", "Formatter", "Extract FormatArgumentValue to MAF-specific formatter.", "Implementer", "Planned", "Unit tests", "proof/SB02/transcripts/formatter-tests.txt"],
  ["SB03", "C07", "Session", "Extract RestoreOrCreateSession and prompt input construction.", "Implementer", "Planned", "Unit and integration tests", "proof/SB03/transcripts/session-tests.txt"],
  ["SB03", "C08", "Session", "Preserve request-scoped attachment stripping.", "Implementer", "Planned", "Attachment tests", "proof/SB03/transcripts/attachment-tests.txt"],
  ["SB03", "C09", "Session", "Preserve provider-managed conversation restoration.", "Implementer", "Planned", "Recovery tests", "proof/SB03/transcripts/recovery-tests.txt"],
  ["SB04", "C10", "Model", "Extract ChatOptions construction and temperature policy.", "Implementer", "Planned", "Unit tests", "proof/SB04/transcripts/model-options-tests.txt"],
  ["SB04", "C11", "Model", "Preserve reasoning effort and unsupported transport diagnostics.", "Implementer", "Planned", "Unit tests", "proof/SB04/transcripts/reasoning-tests.txt"],
  ["SB05", "C12", "Context", "Extract context manifest creation and tool schema estimates.", "Implementer", "Planned", "Unit tests", "proof/SB05/transcripts/context-tests.txt"],
  ["SB05", "C13", "Context", "Prove included/excluded source totals remain stable.", "Implementer", "Planned", "Integration tests", "proof/SB05/transcripts/run-tracking-tests.txt"],
  ["SB06", "C14", "Finalizer", "Extract required-finalizer repair and JSON repair.", "Implementer", "Planned", "Unit tests", "proof/SB06/transcripts/finalizer-repair-tests.txt"],
  ["SB06", "C15", "Finalizer", "Extract streamed finalizer recorder.", "Implementer", "Planned", "Source assertions", "proof/SB06/source-assertions.md"],
  ["SB06", "C16", "Finalizer", "Prove provider failure after valid finalizer persists governed output.", "Implementer", "Planned", "Integration tests", "proof/SB06/transcripts/provider-failure-tests.txt"],
  ["SB06", "C17", "Finalizer", "Prove missing/malformed/multiple finalizer calls still fail.", "Implementer", "Planned", "Negative tests", "proof/SB06/transcripts/finalizer-negative-tests.txt"],
  ["SB07", "C18", "Static scan", "Remove dead MafAgentRuntime helper methods and duplicate paths.", "Implementer", "Planned", "Static scan", "proof/SB07/transcripts/static-scan.txt"],
  ["SB07", "C19", "Static scan", "Reject new catch-all helpers over threshold.", "Architect", "Planned", "Line-count scan", "proof/SB07/transcripts/file-size-scan.txt"],
  ["SB07", "C20", "Build", "Run MAF project build after orchestration slimming.", "Implementer", "Planned", "Build transcript", "proof/SB07/transcripts/maf-build.txt"],
  ["SB08", "C21", "Regression", "Run focused MAF unit tests.", "QA", "Planned", "Test transcript", "proof/SB08/transcripts/unit-tests.txt"],
  ["SB08", "C22", "Regression", "Run focused execution integration tests.", "QA", "Planned", "Test transcript", "proof/SB08/transcripts/integration-tests.txt"],
  ["SB08", "C23", "UI", "Run Playwright /agents and /agents?tab=agents proof.", "QA", "Planned", "Browser transcript and screenshots", "proof/SB08/screenshots/agents-chat-large.png"],
  ["SB08", "C24", "UI", "Run Playwright capability setup proof.", "QA", "Planned", "Browser transcript and screenshots", "proof/SB08/screenshots/capability-setup-large.png"],
  ["SB08", "C25", "UI", "Run Playwright workflow and process shell smoke proof.", "QA", "Planned", "Browser transcript and screenshots", "proof/SB08/screenshots/workflows-large.png"],
  ["SB08", "C26", "Closure", "Complete raw note closure N001-N010.", "QA", "Planned", "Execution report", "reviews/01-execution-report.md"],
  ["SB08", "C27", "Closure", "Run final bundle validator.", "QA", "Planned", "Validator transcript", "proof/SB08/transcripts/final-validator.txt"],
];

const tests = [
  ["Build", "MAF project", "dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --configuration Release", "SB02-SB08", "Proves MAF compiles after each extraction.", "Planned"],
  ["Build", "Web project", "dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release", "SB08", "Proves Blazor host compiles after runtime refactor.", "Planned"],
  ["Unit", "MAF runtime tests", "dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --filter \"FullyQualifiedName~MafAgentRuntime|FullyQualifiedName~AgentFinalizerPolicy\"", "SB02-SB08", "Covers runtime helpers, builders, finalizer policy.", "Planned"],
  ["Integration", "Execution tests", "dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --filter \"FullyQualifiedName~AgentFrameworkExecution\"", "SB03-SB08", "Covers run tracking, recovery, finalizer, context.", "Planned"],
  ["Playwright", "Agent UI", "dotnet test tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --filter \"FullyQualifiedName~AiAgentFlowTests|FullyQualifiedName~AgentCapabilitySetupFlowPlaywrightTests\"", "SB08", "Covers /agents and capability setup UI.", "Planned"],
  ["Playwright", "Workflow/process UI", "dotnet test tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --filter \"FullyQualifiedName~WorkflowShellSmokeTests|FullyQualifiedName~ProcessShellSmokeTests\"", "SB08", "Covers workflow and process shells after runtime refactor.", "Planned"],
  ["Static", "Line count scan", "PowerShell or rg transcript of runtime .cs file line counts.", "SB01, SB07", "Proves the main runtime shrank and no new catch-all appeared.", "Planned"],
  ["Static", "Anti-stub audit", "rg -n \"TODO|NotImplementedException|throw new NotImplemented|return null;\" changed runtime files", "SB01-SB08", "Rejects placeholder extraction.", "Planned"],
];

const uiProof = [
  ["/agents", "Large desktop first", "Navigate, assert Agent Framework shell/tabs visible, capture screenshot, check console errors.", "proof/SB08/screenshots/agents-shell-large.png", "Are tabs visible, readable, and error-free?", "Planned"],
  ["/agents?tab=agents", "Large desktop first", "Navigate, assert agent list/chat runtime surface visible, capture screenshot.", "proof/SB08/screenshots/agents-chat-large.png", "Is the chat/runtime surface usable and free of runtime errors?", "Planned"],
  ["/agents?tab=capabilities&agentId={seed}", "Large desktop first", "Seed agent, navigate capability tab, assert setup/runtime surface visible.", "proof/SB08/screenshots/capability-setup-large.png", "Are capability details visible without broken diagnostics?", "Planned"],
  ["/agents/workflows", "Large desktop first", "Navigate, assert workflow shell visible, capture screenshot.", "proof/SB08/screenshots/workflows-large.png", "Does workflow shell load after runtime wiring changes?", "Planned"],
  ["Process shell smoke route", "Large desktop first", "Run existing process shell smoke and capture process runtime screenshot.", "proof/SB08/screenshots/process-shell-large.png", "Are process runtime/finalizer surfaces error-free?", "Planned"],
  ["Affected routes after UI file changes", "Narrower viewport", "Repeat only if UI/layout files changed.", "proof/SB08/screenshots/*-narrow.png", "Does layout remain readable without clipping?", "N/A"],
];

const risks = [
  ["RISK01", "Finalizer validation order changes.", "Critical", "SB06 semantic gate with negative and positive integration proof.", "Reopen SB06"],
  ["RISK02", "Stable hash format changes accidentally.", "High", "Characterization tests for casing, length, and prefix.", "Reopen SB02"],
  ["RISK03", "Session serialization keeps request-scoped attachments.", "High", "Attachment stripping tests and recovery tests.", "Reopen SB03"],
  ["RISK04", "Model options change provider compatibility.", "High", "Temperature/reasoning tests and provider diagnostics tests.", "Reopen SB04"],
  ["RISK05", "Context manifest totals drift.", "Medium", "Direct manifest tests and execution tracking integration tests.", "Reopen SB05"],
  ["RISK06", "Code moves into a new catch-all file.", "High", "Line-count and symbol scans with threshold gate.", "Reopen SB07"],
  ["RISK07", "UI route loads but runtime state is broken.", "High", "Playwright assertions plus screenshot review, not route-load-only proof.", "Reopen owning subbundle"],
];

const traceability = [
  ["N001", "MafAgentRuntime.cs is too large.", "R02, R10", "SB01, SB07, SB08", "Line-count and static proof."],
  ["N002", "Split by responsibilities and isolate helpers.", "R02, R09, R10", "SB01-SB07", "Responsibility map and extraction proof."],
  ["N003", "Finalizers as drivers/strategies/helpers.", "R03, R10", "SB06, SB08", "Finalizer semantic proof."],
  ["N004", "ComputeStableHash in whole-project helpers.", "R04", "SB02", "Shared helper tests and dependency scan."],
  ["N005", "FormatArgumentValue as MAF helper.", "R05", "SB02", "MAF formatter tests."],
  ["N006", "Partial classes still mix responsibilities.", "R02, R09", "SB01, SB07", "Partial/catch-all static scan."],
  ["N007", "ModelParameters as builder.", "R07", "SB04", "Model builder tests."],
  ["N008", "Session and context manifest builders.", "R06, R08", "SB03, SB05", "Session/context builder tests."],
  ["N009", "Prepare bundle only.", "R01", "SB01", "No production code changes in preparation."],
  ["N010", "Use xlsx detailed checklists including UI testing.", "R11, R12", "SB01, SB08", "Workbook and UI proof plan."],
];

requirements.push(
  ["R13", "Repair local-provider agent chat so Local Ollama agents send provider-compatible local models.", "SB09", "API and UI chat proof show Local Ollama runs completing with model gemma4-12b-256k.", "Completed"],
  ["R14", "Preserve supported/custom local model choices while falling back only from managed-seed OpenAI defaults.", "SB09", "Unit tests prove fallback and preservation behavior.", "Completed"],
  ["R15", "Repair local Playwright MCP setup and runtime launch/framing for agent chat.", "SB09", "Setup proof discovers schemas and runtime receipts include browser_navigate/browser_snapshot.", "Completed"],
  ["R16", "Prove the provider/MCP repair through real app UI and API flows, not mocked providers or fake tools.", "SB09", "Live API details, screenshots, persisted execution details, and cleanup proof exist.", "Completed"],
  ["R17", "Update bundle/checklist evidence for the follow-up provider/MCP regression.", "SB09", "SB09 README, manifest, invariants, traceability, report, and workbook are updated.", "Completed"],
);

subbundles.push(
  ["SB09", "Local Provider Agent Chat Repair", "Yes", "SB08", "Local Ollama agent chat and Playwright MCP runtime proof pass in API and UI", "proof/SB09/manifest.md"],
);

checklist.push(
  ["SB09", "C28", "Provider", "Reproduce and isolate Local Ollama agent-chat model mismatch against provider health/workflow behavior.", "Implementer", "Completed", "Root-cause proof", "proof/SB09/manifest.md"],
  ["SB09", "C29", "Provider", "Add managed-seed OpenAI model fallback to Local Ollama provider default without changing supported/custom local models.", "Implementer", "Completed", "Unit tests", "proof/SB09/transcripts/focused-unit-tests.txt"],
  ["SB09", "C30", "Model", "Wire MafModelParametersBuilder through the shared provider fallback helper.", "Implementer", "Completed", "Source assertions", "proof/SB09/transcripts/source-assertions.txt"],
  ["SB09", "C31", "MCP setup", "Seed Playwright MCP with newlineDelimitedJson framing, timeout, and reusable cached launcher resolution.", "Implementer", "Completed", "Integration and setup proof", "proof/SB09/transcripts/focused-integration-tests.txt"],
  ["SB09", "C32", "MCP runtime", "Route local stdio MCP capabilities through IMcpRuntimeClient and preserve input schemas.", "Implementer", "Completed", "Runtime composition test", "proof/SB09/transcripts/focused-unit-tests.txt"],
  ["SB09", "C33", "Build", "Run MAF MCP project build, MAF runtime build, and web app build.", "Implementer", "Completed", "Build transcripts", "proof/SB09/transcripts/web-build.txt"],
  ["SB09", "C34", "API", "Run Local Ollama agent-chat API proof and project-structure API proof.", "QA", "Completed", "Run detail JSON", "proof/SB09/api-local-ollama-run-detail.json"],
  ["SB09", "C35", "API", "Run Local Ollama plus Playwright MCP API proof with browser tool receipts.", "QA", "Completed", "Run detail JSON", "proof/SB09/api-local-ollama-playwright-mcp-run-detail.json"],
  ["SB09", "C36", "UI", "Run agents-page UI chat proof for Local Ollama.", "QA", "Completed", "Browser screenshot and run detail", "proof/SB09/browser-ui-local-ollama-chat-run-detail.json"],
  ["SB09", "C37", "UI", "Run UI-started Local Ollama plus Playwright MCP tool proof.", "QA", "Completed", "Browser screenshot and run detail", "proof/SB09/screenshots/browser-ui-local-ollama-playwright-mcp-completed.png"],
  ["SB09", "C38", "Cleanup", "Delete disposable API/UI agents and verify they no longer exist.", "QA", "Completed", "Cleanup proof", "proof/SB09/temp-ui-local-ollama-playwright-agent-cleanup.json"],
  ["SB09", "C39", "Audit", "Run anti-stub audit and proof JSON assertions.", "QA", "Completed", "Audit transcript", "proof/SB09/transcripts/anti-stub-audit.txt"],
  ["SB09", "C40", "Closure", "Update SB09 manifest, semantic invariants, execution report, traceability, and workbook.", "QA", "Completed", "Bundle files", "proof/SB09/manifest.md"],
);

tests.push(
  ["Build", "MAF MCP runtime", "dotnet build src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/CanDoItAll.AgentFramework.Mcp.csproj --no-restore", "SB09", "Proves local stdio MCP runtime changes compile.", "Completed"],
  ["Build", "MAF runtime", "dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore -p:BuildProjectReferences=false", "SB09", "Proves MAF runtime/provider/MCP composition compiles.", "Completed"],
  ["Build", "Web app", "dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore", "SB09", "Proves the app host compiles before live proof.", "Completed"],
  ["Unit", "Provider fallback and MCP composition", "dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -p:BuildProjectReferences=false --filter \"FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests.Local_mcp_capability_uses_runtime_client_factory_and_exposes_invocable_schema_tools|FullyQualifiedName~McpRuntimeContractsTests|FullyQualifiedName~CapabilityTemplateSeedMaterializationTests|FullyQualifiedName~ManagedSeedProviderFallbacksTests\"", "SB09", "Covers fallback behavior, seed materialization, runtime MCP schema/tool composition.", "Completed"],
  ["Integration", "Playwright MCP seed", "dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -p:BuildProjectReferences=false --filter \"FullyQualifiedName~AgentFrameworkWorkspaceSeedIntegrationTests.Organization_workspace_seeds_playwright_mcp_for_ui_delivery_agents\"", "SB09", "Covers seeded Playwright MCP configuration.", "Completed"],
  ["Live API", "Local Ollama and MCP", "PowerShell assertions over proof/SB09/*.json from http://127.0.0.1:5032", "SB09", "Verifies persisted provider/model/tool receipts from real app runs.", "Completed"],
);

uiProof.push(
  ["/agents?tab=chat&agentId={local-ollama-agent}", "Large desktop first", "Send a Local Ollama response-marker prompt and assert completed UI/run detail.", "proof/SB09/screenshots/browser-ui-temp-local-ollama-playwright-chat-initial.png", "Did the UI chat complete through Local Ollama instead of only provider health?", "Completed"],
  ["/agents?tab=chat&agentId={local-ollama-mcp-agent}", "Large desktop first", "Send a prompt requiring browser_navigate and browser_snapshot, then assert completed UI/run detail/tool receipts.", "proof/SB09/screenshots/browser-ui-local-ollama-playwright-mcp-completed.png", "Does visible UI prove a real completed browser-tool run?", "Completed"],
);

risks.push(
  ["RISK08", "Agent chat keeps a managed OpenAI model after switching to Local Ollama, so Ollama is never called.", "Critical", "Provider fallback helper only remaps known managed seed OpenAI models to the Local Ollama default.", "Reopen SB09"],
  ["RISK09", "Broad fallback changes an intentional custom local model.", "High", "Unit tests prove supported/custom local model preservation.", "Reopen SB09"],
  ["RISK10", "MCP setup passes but runtime agent chat hangs or exposes tools without schemas.", "Critical", "Use project-owned local stdio runtime client, newlineDelimitedJson framing, schema preservation tests, and live receipts.", "Reopen SB09"],
  ["RISK11", "UI proof is faked through API-only artifacts.", "High", "Require browser screenshot/DOM proof plus persisted run details from UI-started chat.", "Reopen SB09"],
);

traceability.push(
  ["N011", "Analyze provider trouble and repair it.", "R13-R17", "SB09", "Root-cause analysis, focused tests, live API/UI proof."],
  ["N012", "gptoss20b64k project-structure chat did not respond and GPU/model did not load.", "R13, R16", "SB09", "Local Ollama project-structure/agent-chat run detail proof."],
  ["N013", "Provider setup health check worked and loaded the model.", "R13", "SB09", "Repair compares setup/workflow success with agent-chat model resolution."],
  ["N014", "Financial Manager is configured to local Ollama provider.", "R13, R14", "SB09", "Provider default fallback from managed seed model names."],
  ["N015", "gemma4-12b-256k had the same agent-chat result.", "R13, R14", "SB09", "Live proof uses gemma4-12b-256k as the actual runtime model."],
  ["N016", "Agents-page chat also did not send anything to Ollama.", "R13, R16", "SB09", "Agents-page UI proof completes with Local Ollama."],
  ["N017", "Workflow simple LLM call worked with local Ollama.", "R13, R16", "SB09", "Fix remains scoped to agent chat."],
  ["N018", "Use Playwright MCP through UI chat and do not fake tests.", "R15-R17", "SB09", "Live setup proof, UI screenshot/DOM proof, and persisted MCP tool receipts."],
);

for (const row of requirements) {
  if (row[4] === "Planned") {
    row[4] = "Completed";
  }
}

for (const row of checklist) {
  if (row[5] === "Planned") {
    row[5] = "Completed";
  }
}

for (const row of tests) {
  if (row[5] === "Planned") {
    row[5] = "Completed";
  }
}

for (const row of uiProof) {
  if (row[5] === "Planned") {
    row[5] = "Completed";
  }
}

const overview = addSheet("Overview");
writeTitle(overview, "MAF Runtime Responsibility Split", "Implementation and follow-up local-provider repair checklist. Status cells reflect SB01-SB09 closure evidence.", "E");
overview.getRange("A4:B8").values = [
  ["Bundle path", "codex/bundles/maf-runtime-responsibility-split"],
  ["Profile", "initiative"],
  ["Execution state", "Implemented through SB09"],
  ["Critical path", "Inventory -> helpers -> builders -> finalizer -> orchestration -> regression/UI -> local provider/MCP repair"],
  ["Prepared date", "2026-07-04"],
];
overview.getRange("A10:A14").values = [
  ["Requirement count"],
  ["Critical subbundles"],
  ["Checklist rows"],
  ["UI proof routes"],
  ["Open planned rows"],
];
overview.getRange("B10:B14").values = [
  [requirements.length],
  [subbundles.filter(row => row[2] === "Yes").length],
  [checklist.length],
  [uiProof.length],
  [checklist.filter(row => row[5] === "Planned").length],
];
overview.getRange("A4:A8").format = { fill: colors.grayFill, font: { bold: true } };
overview.getRange("A10:B14").format.borders = { preset: "all", style: "thin", color: colors.line };
overview.getRange("A10:A14").format = { fill: "#EEF6FF", font: { bold: true } };
setColumnWidths(overview, [26, 64, 16, 16, 16]);

const reqSheet = addSheet("Requirements");
writeTitle(reqSheet, "Normalized Requirements", "Requirement-to-owner checklist from bundle requirements/01-normalized-requirements.md.", "E");
writeTable(reqSheet, "A4", ["ID", "Requirement", "Owner", "Acceptance Signal", "Status"], requirements, "RequirementsTable");
setColumnWidths(reqSheet, [12, 58, 18, 58, 18]);

const subSheet = addSheet("Subbundles");
writeTitle(subSheet, "Subbundle Gates", "Dependency and proof gates for the staged implementation.", "F");
writeTable(subSheet, "A4", ["ID", "Title", "Critical", "Prerequisites", "Closure Gate", "Proof Manifest"], subbundles, "SubbundlesTable");
setColumnWidths(subSheet, [12, 36, 14, 30, 54, 34]);

const checklistSheet = addSheet("Checklist");
writeTitle(checklistSheet, "Detailed Execution Checklist", "Update row status during execution. Each row points to a validation method and durable artifact path.", "H");
writeTable(checklistSheet, "A4", ["Subbundle", "ID", "Category", "Checklist Item", "Owner", "Status", "Validation", "Artifact"], checklist, "ExecutionChecklistTable");
setColumnWidths(checklistSheet, [12, 10, 18, 62, 18, 16, 28, 44]);

const testsSheet = addSheet("Tests");
writeTitle(testsSheet, "Build And Test Plan", "Commands required by the subbundles. Capture transcripts under the referenced proof folders during execution.", "F");
writeTable(testsSheet, "A4", ["Type", "Scope", "Command", "Subbundles", "Purpose", "Status"], tests, "TestsTable");
setColumnWidths(testsSheet, [16, 22, 76, 20, 50, 16]);
testsSheet.getRange("A5:F12").format.rowHeight = 58;

const uiSheet = addSheet("UI Proof");
writeTitle(uiSheet, "UI Proof Plan", "Playwright routes, viewport passes, screenshots, and review questions required after runtime refactoring.", "F");
writeTable(uiSheet, "A4", ["Route", "Viewport", "Actions And Assertions", "Screenshot", "Review Question", "Status"], uiProof, "UIProofTable");
setColumnWidths(uiSheet, [34, 24, 68, 46, 52, 16]);

const riskSheet = addSheet("Risks");
writeTitle(riskSheet, "Risk Register", "Critical refactor risks and the subbundle that must reopen if the risk materializes.", "E");
writeTable(riskSheet, "A4", ["ID", "Risk", "Priority", "Mitigation", "Reopen Trigger"], risks, "RisksTable");
setColumnWidths(riskSheet, [12, 48, 14, 60, 30]);

const traceSheet = addSheet("Traceability");
writeTitle(traceSheet, "Raw Input Traceability", "Every raw note from the request remains mapped to requirements, subbundles, and proof.", "E");
writeTable(traceSheet, "A4", ["Raw Note", "Exact Wording", "Requirements", "Owning Subbundles", "Planned Proof"], traceability, "TraceabilityTable");
setColumnWidths(traceSheet, [12, 58, 24, 28, 54]);

for (const sheet of workbook.worksheets.items) {
  sheet.freezePanes.freezeRows(4);
}

await fs.mkdir(previewDir, { recursive: true });

const inspect = await workbook.inspect({
  kind: "sheet,table",
  maxChars: 6000,
  tableMaxRows: 4,
  tableMaxCols: 8,
});
console.log(inspect.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

for (const sheet of workbook.worksheets.items) {
  const preview = await workbook.render({
    sheetName: sheet.name,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  const bytes = new Uint8Array(await preview.arrayBuffer());
  await fs.writeFile(path.join(previewDir, `${sheet.name.replaceAll(" ", "-").toLowerCase()}.png`), bytes);
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`Saved ${outputPath}`);
