import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const bundleRoot = path.resolve("..");
const outputDir = path.join(bundleRoot, "checklists");
const previewDir = path.join(outputDir, "previews");
await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(previewDir, { recursive: true });

const workbook = Workbook.create();

const palette = {
  navy: "#16324F",
  teal: "#0F766E",
  amber: "#B45309",
  red: "#B91C1C",
  green: "#166534",
  slate: "#334155",
  lightBlue: "#EAF2F8",
  lightTeal: "#E8F5F2",
  lightAmber: "#FFF7ED",
  lightRed: "#FEF2F2",
  lightGray: "#F8FAFC",
  border: "#CBD5E1",
  white: "#FFFFFF",
};

function addSheet(name) {
  const sheet = workbook.worksheets.add(name);
  sheet.showGridLines = false;
  return sheet;
}

function colName(index) {
  let name = "";
  let current = index + 1;
  while (current > 0) {
    const modulo = (current - 1) % 26;
    name = String.fromCharCode(65 + modulo) + name;
    current = Math.floor((current - modulo) / 26);
  }
  return name;
}

function writeTable(sheet, startCell, headers, rows, tableName) {
  const rowCount = rows.length + 1;
  const colCount = headers.length;
  const startCol = startCell.match(/[A-Z]+/)[0];
  const startRow = Number(startCell.match(/\d+/)[0]);
  const startColIndex = startCol.split("").reduce((acc, char) => acc * 26 + char.charCodeAt(0) - 64, 0) - 1;
  const endCol = colName(startColIndex + colCount - 1);
  const endRow = startRow + rowCount - 1;
  const range = `${startCell}:${endCol}${endRow}`;
  sheet.getRange(range).values = [headers, ...rows];
  const headerRange = sheet.getRange(`${startCell}:${endCol}${startRow}`);
  headerRange.format = {
    fill: palette.navy,
    font: { bold: true, color: palette.white },
    wrapText: true,
  };
  sheet.getRange(range).format.borders = { preset: "outside", style: "thin", color: palette.border };
  if (rows.length > 0) {
    sheet.getRange(`${startCol}${startRow + 1}:${endCol}${endRow}`).format = {
      fill: palette.white,
      wrapText: true,
      verticalAlignment: "top",
    };
  }
  sheet.tables.add(range, true, tableName);
  return { range, endRow, endCol };
}

function setWidths(sheet, widths) {
  widths.forEach((width, index) => {
    sheet.getRange(`${colName(index)}:${colName(index)}`).format.columnWidth = width;
  });
}

function title(sheet, text, subtitle = null) {
  sheet.getRange("A1:H1").merge();
  sheet.getRange("A1").values = [[text]];
  sheet.getRange("A1").format = {
    fill: palette.navy,
    font: { bold: true, color: palette.white, size: 16 },
    rowHeight: 30,
  };
  if (subtitle) {
    sheet.getRange("A2:H2").merge();
    sheet.getRange("A2").values = [[subtitle]];
    sheet.getRange("A2").format = {
      fill: palette.lightBlue,
      font: { color: palette.slate },
      wrapText: true,
      rowHeight: 34,
    };
  }
}

const summary = addSheet("Summary");
title(summary, "MAF 1.13 Conservative Update Bundle", "Preparation workbook for phase execution, evidence capture, and architecture validation.");
summary.getRange("A4:D4").values = [["Metric", "Count Type", "Current", "Meaning"]];
summary.getRange("A4:D4").format = { fill: palette.teal, font: { bold: true, color: palette.white } };
summary.getRange("A5:D11").values = [
  ["Total checklist items", "Rows", 0, "Every actionable row in the phase checklist."],
  ["Passed items", "Passed", 0, "Rows marked Passed during implementation."],
  ["Blocked items", "Blocked", 0, "Rows blocking downstream work."],
  ["Critical foundations", "Critical", 0, "Foundation rows that gate later phases."],
  ["Package decisions", "Packages", 0, "Package rows that need before/after evidence."],
  ["Validation commands", "Commands", 0, "Commands or source scans expected during execution."],
  ["Open risks", "Risks", 0, "Risks still open during execution."],
];
summary.getRange("A5:D11").format.borders = { preset: "all", style: "thin", color: palette.border };
summary.getRange("A13:H13").merge();
summary.getRange("A13").values = [["Execution Rule"]];
summary.getRange("A13").format = { fill: palette.amber, font: { bold: true, color: palette.white } };
summary.getRange("A14:H18").merge(true);
summary.getRange("A14:H18").values = [
  ["Run subbundles in order. SB01 through SB04 are critical foundations; do not run broad validation before the architecture checkpoint passes."],
  ["No package or source implementation has been performed in this preparation turn."],
  ["Every skipped validation must include a concrete environment reason, not a generic deferral."],
  ["Preview package decisions must be based on current NuGet CLI output at implementation time."],
  ["Do not solve package breaks by adding direct process runtime tools or expanding process APIs."],
];
summary.getRange("A14:H18").format = { fill: palette.lightAmber, wrapText: true };
setWidths(summary, [28, 18, 14, 80, 12, 12, 12, 12]);
summary.freezePanes.freezeRows(4);

const phaseRows = [
  ["SB01-01", "SB01 Inventory", "Critical", "Record branch and git status", "Implementation agent", "git status transcript", "Not started", "Dirty unrelated work is not understood", "proof/SB01/transcripts/git-status.md"],
  ["SB01-02", "SB01 Inventory", "Critical", "Record dotnet SDK and restore environment", "Implementation agent", "dotnet --info transcript", "Not started", ".NET 10 SDK unavailable", "proof/SB01/transcripts/dotnet-info.md"],
  ["SB01-03", "SB01 Inventory", "Critical", "List direct package references in MAF, workflow adapter, hosting, and tooling projects", "Implementation agent", "dotnet list package transcripts", "Not started", "Package graph unknown", "proof/SB01/transcripts/package-list.md"],
  ["SB01-04", "SB01 Inventory", "Critical", "Run source search for Microsoft.Agents.AI and Microsoft.Extensions.AI package references", "Implementation agent", "rg transcript", "Not started", "Unowned package refs found", "proof/SB01/transcripts/package-search.md"],
  ["SB01-05", "SB01 Inventory", "Critical", "Capture baseline restore and build result", "Implementation agent", "restore/build transcripts", "Not started", "Pre-existing failure not separated", "proof/SB01/transcripts/baseline-build.md"],
  ["SB01-06", "SB01 Inventory", "Critical", "Record focused test candidates from current source", "Implementation agent", "rg class scan transcript", "Not started", "Validation target unknown", "proof/SB01/transcripts/test-discovery.md"],
  ["SB02-01", "SB02 Packages", "Critical", "Re-run outdated package check with prerelease enabled", "Implementation agent", "NuGet CLI transcript", "Not started", "Preview decision guessed", "proof/SB02/transcripts/outdated.md"],
  ["SB02-02", "SB02 Packages", "Critical", "Update stable MAF packages to 1.13.0 only", "Implementation agent", "package diff", "Not started", "Unrelated package changed", "proof/SB02/transcripts/package-diff.md"],
  ["SB02-03", "SB02 Packages", "Critical", "Evaluate A2A preview in MAF and Hosting projects", "Implementation agent", "A2A decision row", "Not started", "A2A compatibility unknown", "proof/SB02/transcripts/a2a-decision.md"],
  ["SB02-04", "SB02 Packages", "Critical", "Evaluate Mem0 preview package availability", "Implementation agent", "Mem0 decision row", "Not started", "Mem0 version guessed", "proof/SB02/transcripts/mem0-decision.md"],
  ["SB02-05", "SB02 Packages", "Critical", "Align dependency-floor packages only when restore/build requires it", "Implementation agent", "restore/build evidence", "Not started", "Latest-version chase detected", "proof/SB02/transcripts/dependency-floor.md"],
  ["SB02-06", "SB02 Packages", "Critical", "Run restore after package-only diff", "Implementation agent", "restore transcript", "Not started", "Restore failure not explained", "proof/SB02/transcripts/restore.md"],
  ["SB03-01", "SB03 Adapter Fixes", "Critical", "Capture failing-first build after package update", "Implementation agent", "build failure transcript", "Not started", "No compile-error baseline", "proof/SB03/transcripts/build-failing-first.md"],
  ["SB03-02", "SB03 Adapter Fixes", "Critical", "Fix package API drift in runtime/session/streaming/capability/workflow adapter seams", "Implementation agent", "changed-file source assertions", "Not started", "Fix outside adapter seam", "proof/SB03/source-assertions.md"],
  ["SB03-03", "SB03 Adapter Fixes", "Critical", "Preserve approvals, finalizers, structured output, provider gates, session state, context manifests", "Implementation agent", "semantic invariant proof", "Not started", "Governance behavior weakened", "proof/SB03/semantic-invariants.md"],
  ["SB03-04", "SB03 Adapter Fixes", "Critical", "Add direct unit tests for any new helper or adapter", "Implementation agent", "test transcript", "Not started", "Helper lacks direct test", "proof/SB03/transcripts/helper-tests.md"],
  ["SB03-05", "SB03 Adapter Fixes", "Critical", "Run passing build", "Implementation agent", "build transcript", "Not started", "Build fails", "proof/SB03/transcripts/build-passing.md"],
  ["SB03-06", "SB03 Adapter Fixes", "Critical", "Run anti-stub/source audit", "Implementation agent", "anti-stub transcript", "Not started", "TODO/NotImplemented on production path", "proof/SB03/transcripts/anti-stub.md"],
  ["SB04-01", "SB04 Architecture", "Critical", "Review git diff stat and changed files", "Architect reviewer", "diff transcript", "Not started", "Diff too broad", "proof/SB04/transcripts/diff-stat.md"],
  ["SB04-02", "SB04 Architecture", "Critical", "Run source scans for ProcessAgentRuntimeToolProvider and process route expansion", "Architect reviewer", "rg transcript", "Not started", "New direct process surface", "proof/SB04/transcripts/process-scan.md"],
  ["SB04-03", "SB04 Architecture", "Critical", "Run stale stable MAF 1.8 source scan", "Architect reviewer", "rg transcript", "Not started", "Stable MAF 1.8 reference remains", "proof/SB04/transcripts/maf18-scan.md"],
  ["SB04-04", "SB04 Architecture", "Critical", "Check partial-class policy and helper testability", "Architect reviewer", "gate note", "Not started", "Fake separation accepted", "reviews/csharp-architecture-gate.md"],
  ["SB04-05", "SB04 Architecture", "Critical", "Run CodeAnalytics dependency proof if project references changed", "Architect reviewer", "CodeAnalytics transcript", "Not started", "New cycle or wrong direction", "proof/SB04/transcripts/dependencies.md"],
  ["SB05-01", "SB05 Validation", "Validation", "Run focused unit tests for runtime, providers, finalizers, tool composition, workflow, process dispatch", "QA agent", "unit test transcript", "Not started", "Focused behavior unproven", "proof/SB05/transcripts/focused-unit.md"],
  ["SB05-02", "SB05 Validation", "Validation", "Run focused integration tests for AgentFramework, process, project-structure bridge", "QA agent", "integration transcript", "Not started", "Integration behavior unproven", "proof/SB05/transcripts/focused-integration.md"],
  ["SB05-03", "SB05 Validation", "Validation", "Run broad unit, integration, and component tests when feasible", "QA agent", "broad test transcripts", "Not started", "Broad regressions unknown", "proof/SB05/transcripts/broad-tests.md"],
  ["SB05-04", "SB05 Validation", "Validation", "Run Playwright smoke if environment is ready", "QA agent", "browser analytics row", "Not started", "UI smoke skipped without reason", "proof/SB05/browser/"],
  ["SB05-05", "SB05 Validation", "Validation", "Map replacement tests if exact filters differ", "QA agent", "replacement map", "Not started", "Validation intent lost", "proof/SB05/test-replacement-map.md"],
  ["SB06-01", "SB06 Evidence", "Closure", "Create docs/maf-1.13-update-evidence.md", "Implementation agent", "evidence doc", "Not started", "Merge evidence missing", "docs/maf-1.13-update-evidence.md"],
  ["SB06-02", "SB06 Evidence", "Closure", "Record package before/after and preview decisions", "Implementation agent", "evidence table", "Not started", "Package decision unclear", "docs/maf-1.13-update-evidence.md"],
  ["SB06-03", "SB06 Evidence", "Closure", "Run final scans and git diff --check", "Implementation agent", "scan transcripts", "Not started", "Final hygiene unknown", "proof/SB06/transcripts/final-scans.md"],
  ["SB06-04", "SB06 Evidence", "Closure", "Close raw notes as Solved, Partially solved, or Not solved", "Implementation agent", "raw note closure table", "Not started", "Closure hides gap", "reviews/01-execution-report.md"],
  ["SB06-05", "SB06 Evidence", "Closure", "Run completed-stage bundle validation after implementation", "Implementation agent", "validator transcript", "Not started", "Bundle and proof disagree", "proof/SB06/transcripts/completed-validator.md"],
];

const phase = addSheet("Phase Checklist");
title(phase, "Phase Checklist", "Actionable checklist for package update execution and validation.");
const phaseTable = writeTable(phase, "A4", ["ID", "Phase", "Gate", "Task", "Owner", "Required Evidence", "Status", "Stop Condition", "Artifact Path"], phaseRows, "PhaseChecklist");
setWidths(phase, [14, 22, 14, 55, 24, 34, 18, 42, 42]);
phase.getRange(`G5:G${phaseTable.endRow}`).dataValidation = { rule: { type: "list", values: ["Not started", "In progress", "Passed", "Blocked", "Skipped", "N/A"] } };
phase.freezePanes.freezeRows(4);

const packageRows = [
  ["CanDoItAll.AgentFramework.Maf", "Microsoft.Agents.AI", "1.8.0", "1.13.0", "Update", "Stable MAF package", "SB02", "proof/SB02/transcripts/restore.md"],
  ["CanDoItAll.AgentFramework.Maf", "Microsoft.Agents.AI.OpenAI", "1.8.0", "1.13.0", "Update", "Stable MAF package", "SB02", "proof/SB02/transcripts/restore.md"],
  ["CanDoItAll.AgentFramework.Maf", "Microsoft.Agents.AI.Workflows", "1.8.0", "1.13.0", "Update", "Stable MAF package", "SB02", "proof/SB02/transcripts/restore.md"],
  ["CanDoItAll.AgentFramework.Maf", "Microsoft.Agents.AI.A2A", "1.8.0-preview.260528.1", "1.13.0-preview.260703.1 if current CLI confirms", "Conditional", "Preview package; recheck at implementation", "SB02", "proof/SB02/transcripts/a2a-decision.md"],
  ["CanDoItAll.AgentFramework.Maf", "Microsoft.Agents.AI.Mem0", "1.0.0-preview.251028.1", "Do not guess", "Keep or isolate", "Preparation CLI reported not found", "SB02", "proof/SB02/transcripts/mem0-decision.md"],
  ["CanDoItAll.AgentFramework.Workflows.MafAdapter", "Microsoft.Agents.AI", "1.8.0", "1.13.0", "Update", "Stable MAF package", "SB02", "proof/SB02/transcripts/restore.md"],
  ["CanDoItAll.AgentFramework.Workflows.MafAdapter", "Microsoft.Agents.AI.Workflows", "1.8.0", "1.13.0", "Update", "Stable MAF package", "SB02", "proof/SB02/transcripts/restore.md"],
  ["CanDoItAll.AgentFramework.Workflows.MafAdapter", "Microsoft.Extensions.AI.Abstractions", "10.5.1", "10.6.0 if required", "Conditional", "MAF 1.13 dependency floor", "SB02", "proof/SB02/transcripts/dependency-floor.md"],
  ["CanDoItAll.AgentFramework.Workflows.MafAdapter", "Microsoft.Extensions.DependencyInjection.Abstractions", "10.0.7", "10.0.9 if required", "Conditional", "MAF 1.13 dependency floor", "SB02", "proof/SB02/transcripts/dependency-floor.md"],
  ["CanDoItAll.AgentFramework.Hosting", "Microsoft.Agents.AI.Hosting.A2A", "1.8.0-preview.260528.1", "1.13.0-preview.260703.1 if current CLI confirms", "Conditional", "New prep finding", "SB02", "proof/SB02/transcripts/a2a-decision.md"],
  ["CanDoItAll.AgentFramework.Hosting", "Microsoft.Extensions.DependencyInjection.Abstractions", "10.0.7", "10.0.9 only if required", "Conditional", "Do not adopt preview 11", "SB02", "proof/SB02/transcripts/dependency-floor.md"],
  ["CanDoItAll.AgentFramework.Tooling", "Microsoft.Extensions.AI.Abstractions", "10.5.1", "10.6.0 only if required", "Conditional", "Do not chase latest 10.7 automatically", "SB02", "proof/SB02/transcripts/dependency-floor.md"],
];
const pkg = addSheet("Package Matrix");
title(pkg, "Package Matrix", "Package decisions captured from prep plus read-only NuGet CLI evidence.");
writeTable(pkg, "A4", ["Project", "Package", "Current", "Target", "Action", "Rule", "Phase", "Evidence"], packageRows, "PackageMatrix");
setWidths(pkg, [42, 44, 26, 36, 20, 52, 12, 42]);
pkg.freezePanes.freezeRows(4);

const commandRows = [
  ["SB01", "git status --short", "No unexplained changes or documented dirty state", "proof/SB01/transcripts/git-status.md", "Required"],
  ["SB01", "dotnet --info", ".NET SDK environment recorded", "proof/SB01/transcripts/dotnet-info.md", "Required"],
  ["SB01", "rg 'Microsoft\\.Agents\\.AI|Microsoft\\.Extensions\\.AI' src tests tools -g '*.csproj'", "Direct package refs inventoried", "proof/SB01/transcripts/package-search.md", "Required"],
  ["SB02", "dotnet list <project> package --outdated --include-prerelease", "Preview and dependency-floor decisions recorded", "proof/SB02/transcripts/outdated.md", "Required"],
  ["SB02", "dotnet restore CanDoItAll.slnx", "Restore succeeds or package-only blocker recorded", "proof/SB02/transcripts/restore.md", "Required"],
  ["SB03", "dotnet build CanDoItAll.slnx --configuration Release --no-restore", "Build failure then passing proof or blocker", "proof/SB03/transcripts/build-passing.md", "Required"],
  ["SB04", "git diff --stat", "Diff size reviewable", "proof/SB04/transcripts/diff-stat.md", "Required"],
  ["SB04", "rg 'ProcessAgentRuntimeToolProvider|/api/processes/definitions|/api/processes/templates|ProcessManagerTools' src tests docs -g '*.cs' -g '*.md' -g '*.json'", "No new direct process tools/routes", "proof/SB04/transcripts/process-scan.md", "Required"],
  ["SB04", "git diff --check", "No whitespace/conflict marker issues", "proof/SB04/transcripts/diff-check.md", "Required"],
  ["SB05", "dotnet test tests\\Unit\\CanDoItAll.Tests.Unit\\CanDoItAll.Tests.Unit.csproj --configuration Release --filter <focused>", "Focused unit proof", "proof/SB05/transcripts/focused-unit.md", "Required"],
  ["SB05", "dotnet test tests\\Integration\\CanDoItAll.Tests.Integration\\CanDoItAll.Tests.Integration.csproj --configuration Release --filter <focused>", "Focused integration proof", "proof/SB05/transcripts/focused-integration.md", "Required"],
  ["SB05", "dotnet test tests\\Components\\CanDoItAll.Tests.Components\\CanDoItAll.Tests.Components.csproj --configuration Release", "Component proof or skip reason", "proof/SB05/transcripts/components.md", "Recommended"],
  ["SB05", "dotnet test tests\\Playwright\\CanDoItAll.Tests.Playwright\\CanDoItAll.Tests.Playwright.csproj --configuration Release", "Browser smoke or environment skip reason", "proof/SB05/transcripts/playwright.md", "Optional"],
  ["SB06", "final source scans and completed validator", "Evidence and bundle agree", "proof/SB06/transcripts/final-validation.md", "Required"],
];
const commands = addSheet("Validation Commands");
title(commands, "Validation Commands", "Commands and scans required or recommended during execution.");
writeTable(commands, "A4", ["Phase", "Command", "Expected Result", "Evidence Path", "Priority"], commandRows, "ValidationCommands");
setWidths(commands, [12, 80, 48, 44, 16]);
commands.freezePanes.freezeRows(4);

const archRows = [
  ["Boundary ownership", "MAF SDK types stay inside adapter projects", "No Process projects reference MAF implementation packages", "SB04", "reviews/csharp-architecture-gate.md"],
  ["Dependency direction", "No new project references unless gate repaired", "CodeAnalytics dependency proof if references changed", "SB04", "proof/SB04/transcripts/dependencies.md"],
  ["Pattern selection", "Adapter only for external SDK drift", "Pattern record updated for new helper", "SB03/SB04", "architecture/03-csharp-pattern-selection-records.md"],
  ["Testability", "New helper tested without MafAgentRuntime", "Direct unit and negative tests", "SB03/SB05", "architecture/04-csharp-testability-plan.md"],
  ["Partial class policy", "No new final runtime partial", "Diff review and source assertion", "SB04", "reviews/csharp-architecture-gate.md"],
  ["Governance invariants", "Approvals/finalizers/provider gates/session/context preserved", "Focused tests and source assertions", "SB03/SB05", "proof/SB03/semantic-invariants.md"],
];
const arch = addSheet("Architecture Gates");
title(arch, "Architecture Gates", "C# architecture checks that block fake separation and accidental product scope expansion.");
writeTable(arch, "A4", ["Gate", "Rule", "Proof Required", "Phase", "Artifact"], archRows, "ArchitectureGates");
setWidths(arch, [26, 58, 48, 16, 46]);
arch.freezePanes.freezeRows(4);

const riskRows = [
  ["R-001", "Baseline failures mixed with package failures", "High", "SB01 must capture before-change evidence", "Reopen SB01", "Open"],
  ["R-002", "NuGet latest versions broaden update scope", "High", "Package matrix and dependency-floor gate", "Reject SB02", "Open"],
  ["R-003", "A2A preview update breaks restore/runtime", "Medium", "Current CLI proof and focused tests", "Block SB02/SB03", "Open"],
  ["R-004", "Mem0 package not found or incompatible", "High", "Do not guess; keep or isolate only with proof", "Block SB02", "Open"],
  ["R-005", "Compile fixes weaken approvals/finalizers", "High", "Semantic invariants and focused tests", "Reject SB03", "Open"],
  ["R-006", "Runtime partial classes grow further", "Medium", "Partial-class policy gate", "Reject SB04", "Open"],
  ["R-007", "Historical process tool references misread as production tools", "Medium", "Source scan and expected historical mention notes", "Clarify SB04/SB06", "Open"],
  ["R-008", "Optional UI/service tests unavailable", "Medium", "Exact skip reason and no pass claim", "Record SB05 risk", "Open"],
  ["R-009", "Evidence doc diverges from transcripts", "High", "Final closure audit and validator", "Reject SB06", "Open"],
];
const risks = addSheet("Risk Register");
title(risks, "Risk Register", "Risks, mitigations, reopen triggers, and current status.");
writeTable(risks, "A4", ["ID", "Risk", "Severity", "Mitigation", "Trigger", "Status"], riskRows, "RiskRegister");
setWidths(risks, [12, 48, 14, 58, 28, 14]);
risks.getRange("F5:F100").dataValidation = { rule: { type: "list", values: ["Open", "Mitigated", "Blocked", "Closed"] } };
risks.freezePanes.freezeRows(4);

const evidenceRows = [
  ["Raw request", "inputs/00-original-request.md", "Preparation", "Preserved", "Prepared"],
  ["Original prep bundle", "inputs/original-prep", "Preparation", "Copied", "Prepared"],
  ["CodeAnalytics snapshot", "snap-20260707234748-ac72a0ea", "Preparation", "Recorded", "Prepared"],
  ["Prepared validator", "validate_bundle.py --stage prepared", "Preparation", "Pass after workbook generation", "Prepared"],
  ["Workbook artifact", "checklists/maf-1.13-phase-checklists.xlsx", "Preparation", "Generated", "Prepared"],
  ["Implementation transcripts", "proof/SBxx/transcripts", "Execution", "Required later", "Not started"],
  ["Final evidence doc", "docs/maf-1.13-update-evidence.md", "SB06", "Required later", "Not started"],
];
const evidence = addSheet("Evidence Index");
title(evidence, "Evidence Index", "Where execution agents must place proof and how current preparation artifacts map.");
writeTable(evidence, "A4", ["Artifact", "Path Or ID", "Phase", "Expectation", "Status"], evidenceRows, "EvidenceIndex");
setWidths(evidence, [28, 52, 18, 42, 18]);
evidence.freezePanes.freezeRows(4);

summary.getRange("C5:C11").values = [
  [phaseRows.length],
  [phaseRows.filter((row) => row[6] === "Passed").length],
  [phaseRows.filter((row) => row[6] === "Blocked").length],
  [phaseRows.filter((row) => row[2] === "Critical").length],
  [packageRows.length],
  [commandRows.length],
  [riskRows.filter((row) => row[5] === "Open").length],
];

const summaryCheck = await workbook.inspect({
  kind: "table",
  range: "Summary!A4:D11",
  tableMaxRows: 12,
  tableMaxCols: 4,
  maxChars: 4000,
});
console.log(summaryCheck.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

for (const sheetName of ["Summary", "Phase Checklist", "Package Matrix", "Validation Commands", "Architecture Gates", "Risk Register", "Evidence Index"]) {
  const preview = await workbook.render({
    sheetName,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  const bytes = new Uint8Array(await preview.arrayBuffer());
  await fs.writeFile(path.join(previewDir, `${sheetName.replaceAll(" ", "-").toLowerCase()}.png`), bytes);
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(path.join(outputDir, "maf-1.13-phase-checklists.xlsx"));
console.log(`Saved ${path.join(outputDir, "maf-1.13-phase-checklists.xlsx")}`);
