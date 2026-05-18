import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const bundleRoot = path.resolve("C:/repositories/CanDoItAll/codex/bundles/cognitive-memory-followup-lb4u-validation-refactor");
const outputPath = path.join(bundleRoot, "checklists", "cognitive-memory-followup-control.xlsx");

const statusValues = ["Ready", "In Progress", "Completed", "Blocked", "Needs Repair", "Pending"];

const sheets = [
  {
    name: "Summary",
    widths: [220, 180, 480, 220, 220],
    rows: [
      ["Cognitive Memory Follow-Up Control Workbook", null, null, null, null],
      ["Prepared date", "2026-05-18", "Bundle", "cognitive-memory-followup-lb4u-validation-refactor", null],
      ["Main goal", "Finish and validate cognitive memory with realistic staged LB4U data, OpenAI, Ollama, and maintainability refactor.", null, null, null],
      ["Key rule", "LB4U sources are read-only. Exclude routery hesla completely.", null, null, null],
      [null, null, null, null, null],
      ["Metric", "Formula", "Current", "Target", "Notes"],
      ["Subbundles tracked", "COUNTA Phase Gates", null, "11", "All workstreams must close or be explicitly blocked."],
      ["Open critical gates", "COUNTIF not Completed", null, "0", "Ready statuses are not closure."],
      ["High risks tracked", "COUNTIF Risks severity High", null, "0 unresolved", "High risks need owner and mitigation."],
      ["Evidence rows", "COUNTA Evidence Log entries", null, "All gates", "Every validation claim needs a row."],
    ],
  },
  {
    name: "Phase Gates",
    widths: [260, 140, 280, 160, 320, 360],
    rows: [
      ["Subbundle", "Owner", "Entry Gate", "Status", "Closure Gate", "Evidence Required"],
      ["00-reentry-and-harness-gate", "Codex", "Bundle prepared", "Ready", "Baseline recorded", "git status, build/test plan, API readiness"],
      ["01-implementation-audit-refactor-map", "Codex", "00 complete", "Ready", "Refactor map approved", "source/test inventory, gap map"],
      ["02-lb4u-staged-inputs-secret-safety", "Codex", "00 complete", "Ready", "Manifest and exclusions pass", "manifest, extraction summaries, exclusion tests"],
      ["03-model-profile-token-settings", "Codex", "01 complete", "Ready", "OpenAI/Ollama roles explicit", "model id, token budget, truncation tests"],
      ["04-model-assisted-consolidation", "Codex", "02 and 03 complete", "Ready", "Source-backed candidates pass", "candidate samples, tests, review gate proof"],
      ["05-epistemic-cross-project-knowledge", "Codex", "04 complete", "Ready", "Coverage proposals pass", "scan reports, accept/reject review proof"],
      ["06-probing-feedback-regression-loop", "Codex", "04 complete", "Ready", "Probe improvement pass", "probe sessions, before/after results"],
      ["07-maintainability-file-splits", "Codex", "01 and 04 complete", "Ready", "Refactor tests pass", "before/after file inventory, build/tests, browser if UI touched"],
      ["08-openai-lb4u-validation-cycle", "Codex", "05 and 06 complete", "Ready", "OpenAI multi-cycle pass", "operation ids, snapshots, probe summaries"],
      ["09-ollama-gptoss20b64k-validation", "Codex", "08 complete", "Ready", "Local model pass or actionable block", "model status, output token proof, probe summaries"],
      ["10-api-skill-docs-closure", "Codex", "07 and 09 complete", "Ready", "Completed validator pass", "docs/skill/workbook/execution report"],
    ],
  },
  {
    name: "Requirements",
    widths: [120, 520, 260, 160, 360],
    rows: [
      ["ID", "Requirement", "Primary Subbundle", "Status", "Proof"],
      ["FR-FU-001", "Preserve provenance-first v2 invariants and review-gated canonical truth.", "00,04,06", "Ready", "Invariant checklist and tests"],
      ["FR-FU-010", "Define typed LB4U staged ingestion manifest.", "02", "Ready", "Manifest and workbook stage sheet"],
      ["FR-FU-012", "Exclude routery hesla from all ingestion, prompts, logs, memory, recall, and assets.", "02,08,09", "Ready", "Absence checks"],
      ["FR-FU-020", "Improve consolidation candidate quality beyond shallow classification.", "04", "Ready", "Candidate quality tests"],
      ["FR-FU-024", "Add coverage and gap analysis for business-plan and planning dimensions.", "05", "Ready", "Scan reports"],
      ["FR-FU-030", "Record probe prompts, summaries, sources, feedback, and follow-up actions.", "06", "Ready", "Probe observation records"],
      ["FR-FU-040", "Run main validation with OpenAI gpt-5-mini.", "08", "Ready", "Model profile evidence"],
      ["FR-FU-041", "Run local validation with Ollama gptoss20b64k.", "09", "Ready", "Provider and probe evidence"],
      ["FR-FU-042", "Expose model id, provider profile, max output tokens, timeout, and truncation state.", "03", "Ready", "Settings and operation metadata tests"],
      ["FR-FU-050", "Split oversized files around stable responsibilities after behavior tests exist.", "07", "Ready", "Before/after inventory and tests"],
      ["FR-FU-060", "Update API docs and cognitive-memory API skill after behavior changes.", "10", "Ready", "Docs and skill diff"],
      ["FR-FU-061", "Maintain this workbook as checklist and evidence tracker.", "10", "Ready", "Final workbook"],
    ],
  },
  {
    name: "LB4U Stages",
    widths: [120, 260, 480, 440, 280, 180],
    rows: [
      ["Stage", "Name", "Source Files", "Expected Memories", "Validation Probe", "Status"],
      ["1", "Product Discovery", "LB4U-BP.docx; 2020-06-09-prezentace LB4U.pdf", "Project identity, target users, pain points, value proposition, assumptions.", "What is LB4U and who is it for?", "Ready"],
      ["2", "Workflow And UX", "2020-06-09-prezentace LB4U.pptx; LB4U-BP.docx", "Button press workflow, staff acknowledgement, floor plan UI, mobile/browser use.", "How does the button-to-staff workflow work?", "Ready"],
      ["3", "Architecture And Install", "LB4U-BP.docx; presentation PDF", "Hardware/software stack, router/server assumptions, installation procedure.", "Which components and installation steps are planned?", "Ready"],
      ["4", "Procurement", "Alza nabídka Brano 21.4.xlsx; Alza nabídka Brano 27.4.xlsx", "BOM, candidate devices, infrastructure quantities, cost-planning evidence.", "Which expenses and procurement items should be tracked?", "Ready"],
      ["5", "Custom Button", "LB4U Vývoj vlastního tlačítka.pdf; .pptx; Eagle manufacturing note", "Safety, feedback, waterproofing, cable, connector, manufacturing constraints.", "What custom engineering remains?", "Ready"],
      ["6", "Field Test And Release", "Presentation and business plan", "Pilot sites, release milestones, maintenance and extension ideas.", "What are release risks and validation sites?", "Ready"],
      ["7", "Business Plan Knowledge", "LB4U-BP.docx", "Business-plan structure, marketing, staffing, expenses, release planning, reusable candidate knowledge.", "What should a proper business plan contain from studied sources?", "Ready"],
      ["8", "Probing And Study Loop", "Memory API probe/review/consolidation endpoints", "Improvement after feedback, accepted/rejected recommendations, regression probes.", "Which knowledge is LB4U-specific versus reusable?", "Ready"],
    ],
  },
  {
    name: "Memory Probes",
    widths: [80, 540, 220, 360, 200, 220],
    rows: [
      ["ID", "Question", "Expected Source Coverage", "Pass Criteria", "Provider", "Result"],
      ["P01", "What is LB4U, who is it for, and what problem does it solve?", "Business plan, product presentation", "Names care/patient-call context and cites sources.", "OpenAI then Ollama", "Pending"],
      ["P02", "How does the LB4U button-to-server-to-staff workflow work?", "Presentation, business plan", "Explains signal, server, devices, acknowledgement.", "OpenAI then Ollama", "Pending"],
      ["P03", "Which hardware and software components are planned?", "Business plan, procurement", "Mentions ESP32/M5Stack, Raspberry Pi, router, backend/frontend/MQTT where supported.", "OpenAI then Ollama", "Pending"],
      ["P04", "What are the customer installation steps?", "Business plan, presentation", "Returns procedure-like steps with provenance.", "OpenAI then Ollama", "Pending"],
      ["P05", "Which parts are custom engineering work?", "Custom button docs", "Mentions button, piezo feedback, waterproofing, cable/connector, manufacturing constraints.", "OpenAI then Ollama", "Pending"],
      ["P06", "What should a proper business plan contain based on studied project materials?", "Business plan plus derived coverage", "Separates source-backed LB4U observations from reusable planning candidate.", "OpenAI", "Pending"],
      ["P07", "What marketing activities, expenses, salaries, or team costs should be tracked?", "Business plan, procurement", "Does not invent unsupported values; identifies tracked dimensions and gaps.", "OpenAI", "Pending"],
      ["P08", "Which knowledge is LB4U-specific and which is reusable?", "Consolidation and epistemic output", "Correct separation and review status.", "OpenAI then Ollama", "Pending"],
    ],
  },
  {
    name: "Validation Matrix",
    widths: [110, 180, 160, 360, 360, 220],
    rows: [
      ["Cycle", "Provider", "Model", "Scope", "Required Evidence", "Status"],
      ["1", "OpenAI", "gpt-5-mini", "Stages 1-3 ingest, consolidate, probe", "operation ids, source-backed answers, snapshot delta", "Pending"],
      ["2", "OpenAI", "gpt-5-mini", "Stages 4-7 ingest, consolidate, review", "candidate records, accepted/rejected reviews", "Pending"],
      ["3", "OpenAI", "gpt-5-mini", "Deeper study and regression probes", "before/after probe summaries, gap closure", "Pending"],
      ["4", "Ollama", "gptoss20b64k", "Core probe parity and token proof", "model status, max output tokens, truncation state", "Pending"],
      ["Final", "All", "All configured", "Build/test/API/UI/doc closure", "test outputs, completed bundle validator, workbook final", "Pending"],
    ],
  },
  {
    name: "Refactor Targets",
    widths: [420, 120, 420, 360, 200],
    rows: [
      ["File", "Approx Lines", "Concern", "Proposed Boundary", "Status"],
      ["Recall/CognitiveMemoryRecallServices.cs", 3015, "Recall orchestration, channels, scoring, context packing coupled.", "Split validation, lexical/vector retrieval, graph expansion, scoring, context pack, diagnostics.", "Ready"],
      ["Advanced/CognitiveMemoryAdvancedServices.cs", 2343, "Probe, self-regulation, professor review, epistemic drive, cross-project, distributed grouped.", "Split by advanced capability.", "Ready"],
      ["Pages/CognitiveMemoryPage.razor.cs", 1850, "Large UI orchestration and state.", "Extract view models/services and focused components.", "Ready"],
      ["Pages/CognitiveMemoryPage.razor", 1450, "Large markup surface.", "Extract component wrappers using existing component library.", "Ready"],
      ["ReviewUi/CognitiveMemoryReviewUiService.cs", 1119, "Review UI query, policy, and shaping mixed.", "Separate query/build/policy helpers.", "Ready"],
      ["Consolidation/CognitiveMemoryConsolidationServices.cs", 1085, "Selection, candidate creation, application, reporting coupled.", "Split extraction, generation, policy, application, metrics.", "Ready"],
      ["Settings/CognitiveMemorySettingsServices.cs", 926, "Settings validation/defaults/persistence mixed.", "Split typed validation and persistence helpers.", "Ready"],
      ["Web/Api/CognitiveMemoryApi.cs", 1476, "Large API route map.", "Split route groups without changing routes.", "Ready"],
    ],
  },
  {
    name: "Risks",
    widths: [90, 520, 100, 420, 240],
    rows: [
      ["ID", "Risk", "Severity", "Mitigation", "Owner"],
      ["R01", "Generic model answer appears correct without memory provenance.", "High", "Probe validation must inspect context sources and review state.", "Codex"],
      ["R02", "routery hesla leaks into ingestion, prompts, or recall.", "High", "Explicit manifest exclusion and absence checks.", "Codex"],
      ["R03", "Ollama silently truncates long answers.", "High", "Expose max output tokens and truncation state.", "Codex"],
      ["R04", "Refactor churn breaks route or persistence contracts.", "High", "Behavior tests before file splits; route smoke after split.", "Codex"],
      ["R05", "Single LB4U project overgeneralizes business-plan knowledge.", "Medium", "Keep reusable knowledge review-gated and source-supported.", "Codex"],
      ["R06", "Spreadsheet/PDF/PPT extraction misses important tables or slide context.", "Medium", "Source-specific tests and manual probe observations.", "Codex"],
      ["R07", "Provider secrets leak into logs.", "High", "Mask sensitive values and avoid recording credentials.", "Codex"],
    ],
  },
  {
    name: "Evidence Log",
    widths: [130, 160, 220, 420, 420, 160],
    rows: [
      ["Date", "Subbundle", "Evidence Type", "Command Or Endpoint", "Result Summary", "Status"],
      ["2026-05-18", "Bundle Prep", "Prepared validator", "validate_bundle.py --profile initiative --stage prepared", "Bundle is valid for stage prepared.", "Completed"],
    ],
  },
  {
    name: "References",
    widths: [260, 620, 340, 200],
    rows: [
      ["Reference Type", "Path Or Identifier", "Use", "Sensitivity"],
      ["Original bundle", "C:/repositories/CanDoItAll/codex/bundles/cognitive-memory-architecture-v2", "Source contract", "Internal"],
      ["Follow-up bundle", bundleRoot, "Execution plan and state", "Internal"],
      ["LB4U root", "C:/Users/lucys/OneDrive - TechnicInsider/Brano/LB4U", "Read-only source root", "Internal"],
      ["LB4U business plan", "C:/Users/lucys/OneDrive - TechnicInsider/Brano/LB4U/LB4U-BP.docx", "Main semantic source", "Internal"],
      ["Excluded file", "C:/Users/lucys/OneDrive - TechnicInsider/Brano/LB4U/routery hesla", "Exclusion test only; never read or ingest", "Sensitive"],
      ["Codeanalytics snapshot", "snap-20260518225923-20ac6533", "Implementation inventory evidence", "Internal"],
      ["API skill", "C:/Users/lucys/.codex/skills/candoitall-api-cognitive-memory/SKILL.md", "Memory API workflow", "Internal"],
    ],
  },
];

function setTitle(sheet, columnCount) {
  const title = sheet.getRangeByIndexes(0, 0, 1, columnCount);
  title.merge();
  title.format.fill.color = "#1F4E79";
  title.format.font.color = "#FFFFFF";
  title.format.font.bold = true;
  title.format.font.size = 14;
  title.format.horizontalAlignment = "Center";
  title.format.rowHeightPx = 30;
}

function styleSheet(sheet, rows, widths, hasTitle) {
  sheet.showGridLines = false;
  const rowCount = rows.length;
  const colCount = rows[0].length;
  const used = sheet.getRangeByIndexes(0, 0, rowCount, colCount);
  used.format.font.name = "Aptos";
  used.format.font.size = 10;
  used.format.wrapText = true;
  used.format.verticalAlignment = "Top";
  used.format.borders.color = "#D9E2EC";
  used.format.borders.style = "Continuous";

  if (hasTitle) {
    setTitle(sheet, colCount);
  }

  const headerRowIndex = hasTitle ? 5 : 0;
  if (headerRowIndex < rowCount) {
    const header = sheet.getRangeByIndexes(headerRowIndex, 0, 1, colCount);
    header.format.fill.color = "#D9EAF7";
    header.format.font.bold = true;
    header.format.font.color = "#1F2937";
    header.format.horizontalAlignment = "Center";
  }

  for (let index = 0; index < widths.length; index += 1) {
    sheet.getRangeByIndexes(0, index, rowCount, 1).format.columnWidthPx = widths[index];
  }

  sheet.getRangeByIndexes(0, 0, rowCount, colCount).format.rowHeightPx = 42;
  sheet.getRangeByIndexes(0, 0, 1, colCount).format.rowHeightPx = 34;
  sheet.freezePanes.freezeRows(headerRowIndex + 1);
}

function addStatusValidation(sheet, rows) {
  const headers = rows[0];
  const statusIndex = headers.indexOf("Status");
  if (statusIndex < 0 || rows.length < 2) {
    return;
  }

  const range = sheet.getRangeByIndexes(1, statusIndex, rows.length - 1, 1);
  range.dataValidation = {
    rule: {
      type: "list",
      values: statusValues,
    },
  };
}

const workbook = Workbook.create();

for (const spec of sheets) {
  const sheet = workbook.worksheets.add(spec.name);
  sheet.getRangeByIndexes(0, 0, spec.rows.length, spec.rows[0].length).values = spec.rows;
  styleSheet(sheet, spec.rows, spec.widths, spec.name === "Summary");
  addStatusValidation(sheet, spec.rows);
}

const summary = workbook.worksheets.getItem("Summary");
summary.getRange("C7:C10").formulas = [
  ["=COUNTA('Phase Gates'!A2:A12)"],
  ["=COUNTIF('Phase Gates'!D2:D12,\"<>Completed\")"],
  ["=COUNTIF(Risks!C2:C40,\"High\")"],
  ["=COUNTA('Evidence Log'!A2:A100)"],
];

const statusScan = await workbook.inspect({
  kind: "table",
  range: "Summary!A1:E10",
  include: "values,formulas",
  tableMaxRows: 12,
  tableMaxCols: 6,
});
console.log(statusScan.ndjson);

const errorScan = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
  summary: "formula error scan",
});
console.log(errorScan.ndjson);

for (const spec of sheets) {
  const preview = await workbook.render({
    sheetName: spec.name,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  console.log(`${spec.name}: rendered ${preview.size ?? "unknown"} bytes`);
}

await fs.mkdir(path.dirname(outputPath), { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`Saved ${outputPath}`);
