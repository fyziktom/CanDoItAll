import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const bundleRoot = path.resolve("C:/repositories/CanDoItAll/codex/bundles/cognitive-memory-cluster-search-realistic-validation");
const outputPath = path.join(bundleRoot, "checklists", "cognitive-memory-realistic-validation.xlsx");
const followUpBundle = "codex/bundles/cognitive-memory-realistic-validation-architecture-hardening";

function columnName(index) {
  let name = "";
  let value = index;

  while (value > 0) {
    const remainder = (value - 1) % 26;
    name = String.fromCharCode(65 + remainder) + name;
    value = Math.floor((value - 1) / 26);
  }

  return name;
}

function writeSheet(workbook, name, headers, rows) {
  const sheet = workbook.worksheets.add(name);
  const values = [headers, ...rows];
  const lastColumn = columnName(headers.length);
  sheet.getRange(`A1:${lastColumn}${values.length}`).values = values;
  return sheet;
}

const workbook = Workbook.create();

writeSheet(
  workbook,
  "Summary",
  ["Area", "Required proof", "Current status", "Owner subbundle", "Evidence path", "Notes"],
  [
    ["Bundle readiness", "Prepared and completed validator output", "Passed", "00/07", "reviews/01-execution-report.md", "Prepared validator passed before implementation; completed validator passed after closure sync"],
    ["Cluster Search UI", "Unit/component/build/browser proof", "Passed", "01", "proof/tests; proof/browser", "Large-screen 1920x1080 only"],
    ["Workbook", "Workbook generated and verified", "Passed", "02", "checklists/cognitive-memory-realistic-validation.xlsx", "This workbook is the final validation ledger"],
    ["Clean PostgreSQL", "Profile/status proof", "Passed", "03", "proof/api/postgres-clean-profile-create.json", "Clean DB candoitall_cognitive_memory_cluster_validation_20260519"],
    ["Qdrant", "Projection and recall proof", "Passed", "03", "proof/api/qdrant-projection-rebuild.json", "2 projected records; vector stage rag:qdrant:search:2"],
    ["Project source truth transfer", "Transfer/ingestion proof", "Partial with follow-up", "04", "proof/api/database-transfer-projects-result.json", "Projects and structure copied; external file payload transfer is follow-up"],
    ["Dreaming/approvals/probes", "Operation IDs, decisions, traces", "Completed with follow-up", "05", "proof/api", "Probe policy/projection gaps recorded"],
    ["Troubles/follow-up", "Trouble rows and architecture bundle", "Completed", "06", "reviews/02-trouble-log-and-followup.md", followUpBundle],
    ["Final closure", "Build/tests/browser/validators", "Passed", "07", "proof/tests; proof/browser", "No hidden pending notes"],
  ],
);

writeSheet(
  workbook,
  "Checklist",
  ["ID", "Requirement", "Subbundle", "Validation action", "Expected evidence", "Status", "Observed result", "Trouble ID"],
  [
    ["REQ-01", "Add cluster-search tab", "01", "Open Cognitive Memory page and select Cluster Search", "Browser screenshot and component test", "Completed", "Tab loaded with 5 result(s)", ""],
    ["REQ-02", "Server-side paging for cluster search", "01", "Run unit test with filtered counts and page requests", "Unit test output", "Completed", "ClusterSearchResultCount and page 1 item asserted", ""],
    ["REQ-03", "Useful filters and previews", "01", "Search by key text and filter by family/readiness/risk", "Browser screenshot and test assertions", "Completed", "Text/family/readiness/risk filters implemented; previews bounded", ""],
    ["REQ-04", "Large-screen only", "01,07", "Validate at 1920x1080 only", "Browser proof", "Completed", "No medium/small-screen tuning performed", ""],
    ["REQ-05", "Detailed XLSX checklist", "02", "Generate and inspect workbook", "This workbook", "Completed", "Workbook refreshed after execution", ""],
    ["REQ-06", "Clean PostgreSQL and Qdrant validation", "03", "Check API status, profiles, Docker/Qdrant, projection, recall", "API captures", "Completed", "PostgreSQL clean DB active; Qdrant projected and recalled", ""],
    ["REQ-07", "Transfer source truth", "04", "Use supported transfer/ingestion APIs", "Operation IDs or blocker", "Completed with follow-up", "Projects/structure transferred and ingested; file payload transfer missing", "TRB-03"],
    ["REQ-08", "Observe ingestion/dreaming/probes", "05", "Run consolidation, approvals, probes, recall", "Operation IDs and traces", "Completed with follow-up", "Dream/probe validation executed; probe policy and vector option gaps recorded", "TRB-07; TRB-08"],
    ["REQ-09", "Record troubles", "06", "Map every blocker/mismatch", "Troubles sheet", "Completed", "9 trouble rows recorded", ""],
    ["REQ-10", "Create architecture follow-up bundle", "06", "Prepare follow-up bundle with requirements", "Bundle path", "Completed", followUpBundle, ""],
  ],
);

writeSheet(
  workbook,
  "Environment",
  ["Check ID", "Check", "Command/API", "Expected", "Status", "Evidence path", "Notes"],
  [
    ["ENV-01", "Access status", "GET /api/access/status", "Auth requirement known", "Completed", "proof/api/access-status.json", ""],
    ["ENV-02", "Cognitive Memory status", "GET /api/cognitive-memory/status", "Profile/provider health known", "Completed", "proof/api/cognitive-memory-status.json", ""],
    ["ENV-03", "Database profiles", "GET /api/cognitive-memory/database/profiles", "Active and candidate profiles known", "Completed", "proof/api/database-profiles-after-create.json", ""],
    ["ENV-04", "Clean PostgreSQL profile", "Create/switch profile and runtime override", "Clean validation profile active", "Completed", "proof/api/postgres-clean-profile-create.json", "Runtime active profile used configured PostgreSQL override"],
    ["ENV-05", "Qdrant readiness", "docker/qdrant health", "Projection service reachable", "Completed", "proof/api/docker-containers.txt", "candoitall-qdrant healthy"],
    ["ENV-06", "Model/settings", "GET /api/cognitive-memory/settings", "Dream/probe provider path known", "Completed", "proof/api/cognitive-memory-settings.json", ""],
  ],
);

writeSheet(
  workbook,
  "TransferIngestion",
  ["Check ID", "Source truth", "Transfer path", "Ingestion endpoint", "Expected count/detail", "Status", "Operation ID", "Notes"],
  [
    ["TRN-01", "Projects", "Database transfer", "N/A", "13 projects copied", "Completed", "proof/api/database-transfer-projects-result.json", ""],
    ["TRN-02", "Project structures", "Workbench transfer handler", "POST /api/cognitive-memory/ingestion/project-structure", "263 objects and 750 source items", "Completed", "proof/api/project-structure-ingestion-results.json", ""],
    ["TRN-03", "Project files/data", "External-source upload/manifest", "POST /api/cognitive-memory/external-sources/files", "Supported files ingested; excluded secrets skipped", "Follow-up", "", "No first-class transfer path found"],
    ["TRN-04", "Validation source notes", "Bundle evidence", "Recall/probe feedback", "Prior testing facts present or gaps known", "Completed with follow-up", "proof/api/qdrant-recall-ai-tap-source-truth-summary.json", "Memory retained 2 approved AI Tap records; other source truth remains pending review/budget continuation"],
  ],
);

writeSheet(
  workbook,
  "DreamingProbes",
  ["Check ID", "Action", "Endpoint/UI", "Expected behavior", "Status", "Operation/trace ID", "Source-truth comparison", "Notes"],
  [
    ["DRM-01", "Run consolidation", "POST /api/cognitive-memory/consolidation/runs", "Candidates and clusters source-backed", "Completed", "proof/api/consolidation-run-2-restricted.json", "240 source items scanned, 80 candidates created", "Candidate budget stopped before all items"],
    ["DRM-02", "Inspect cluster search", "Cluster Search tab", "Relevant clusters visible through paging", "Completed", "proof/browser/cognitive-memory-cluster-search-final-1920x1080.md", "5 clusters visible with bounded previews", ""],
    ["DRM-03", "Approve useful memories", "POST review-item decisions", "Useful, source-backed items approved", "Completed", "proof/api/review-decisions-controlled.json", "2 concrete AI Tap memories approved", ""],
    ["DRM-04", "Reject noisy memories", "POST review-item decisions", "Noisy/source-poor items rejected", "Completed", "proof/api/dream-aggregate-controlled-rejections.json", "10 generic dream aggregates rejected", "Dream quality follow-up"],
    ["DRM-05", "Probe missing facts", "POST probe session/turn/feedback", "Probe summaries include source references", "Completed with follow-up", "proof/api/probe-feedback-policy-gap.json", "Probe turn selected no candidates despite restricted start policy", "Probe policy and vector options dropped"],
    ["DRM-06", "Recall validation", "POST /api/cognitive-memory/recall", "Answers align with source truth", "Completed", "proof/api/qdrant-recall-ai-tap-source-truth-summary.json", "2 candidates, 2 sections, 4 source refs", "Qdrant vector stage proven"],
    ["DRM-07", "Longer run observation", "Repeated cycles", "Behavior trends recorded", "Follow-up", followUpBundle, "Need resumable orchestration", "Recorded as architecture work"],
  ],
);

writeSheet(
  workbook,
  "ClusterSearchUI",
  ["Check ID", "Scenario", "Input/filter", "Expected", "Status", "Evidence", "Notes"],
  [
    ["UI-01", "Open tab", "Cluster Search", "Tab content loads without errors", "Completed", "proof/browser/cognitive-memory-cluster-search-final-1920x1080.png", ""],
    ["UI-02", "Text search", "Known key/display text", "Filtered results and total count update", "Completed", "proof/tests/unit-cognitive-memory-review-quality.log", ""],
    ["UI-03", "Key family filter", "SemanticTopic", "Only matching clusters returned", "Completed", "proof/tests/unit-cognitive-memory-review-quality.log", ""],
    ["UI-04", "Readiness/risk filter", "AggregateReady/Low", "Only matching clusters returned", "Completed", "proof/tests/unit-cognitive-memory-review-quality.log", ""],
    ["UI-05", "Paging", "ClusterSearchResults page request", "Loads requested page only", "Completed", "proof/tests/unit-cognitive-memory-review-quality.log", ""],
    ["UI-06", "Large screen", "1920x1080", "No overlap/clipping in operator layout", "Completed", "proof/browser/cognitive-memory-cluster-search-final-panel-1920x1080.png", "Large-screen only"],
  ],
);

writeSheet(
  workbook,
  "Troubles",
  ["Trouble ID", "Area", "Observed issue", "Impact", "Evidence", "Recommended fix", "Follow-up requirement", "Status"],
  [
    ["TRB-01", "Host startup", "Static asset serving failed in no-build/production-like startup", "Validation host ambiguity", "proof/host/web-run-stdout.log", "Add startup diagnostics and supported host runbook", "ARCH-01", "Recorded"],
    ["TRB-02", "Database profile clarity", "Runtime override needed extra proof", "Wrong-profile validation risk", "proof/api/clean-active-status.json", "Expose active profile source/database/override reason", "ARCH-02", "Recorded"],
    ["TRB-03", "Transfer completeness", "External file payload transfer is not first-class", "Incomplete source-truth replay", "proof/api/database-transfer-preview.json", "Add file/data manifest transfer", "ARCH-03", "Recorded"],
    ["TRB-04", "Restricted source truth", "Default consolidation scanned 0 restricted project-structure items", "Silent source-truth miss", "proof/api/consolidation-run-1.json", "Add warnings and explicit controls", "ARCH-04", "Recorded"],
    ["TRB-05", "Budget continuation", "Candidate budget stopped before all items evaluated", "Long validations need continuation", "proof/api/consolidation-run-2-restricted.json", "Add cycle IDs, cursors, metrics", "ARCH-05; ARCH-10", "Recorded"],
    ["TRB-06", "Dream quality", "Source-mapped aggregates were too generic", "No useful approval target", "proof/api/dream-aggregate-controlled-rejections.json", "Improve aggregate generation and quality gates", "ARCH-06", "Recorded"],
    ["TRB-07", "Probe policy", "Probe turns drop restricted session policy", "Restricted probe validation untrustworthy", "proof/api/probe-turn-restricted-ask.json", "Persist and reuse session policy", "ARCH-07", "Recorded"],
    ["TRB-08", "Probe vector recall", "Probe turns omit projection options", "Probe misses Qdrant behavior", "proof/api/probe-turn-restricted-ask.json", "Pass projection options through probe ask", "ARCH-08", "Recorded"],
    ["TRB-09", "Qdrant diagnostics", "Qdrant needs explicit options for projection/recall", "Weak operator diagnostics", "proof/api/qdrant-projection-rebuild.json", "Add projection defaults and health summaries", "ARCH-09", "Recorded"],
  ],
);

writeSheet(
  workbook,
  "FollowUpArchitecture",
  ["Item ID", "Observed evidence", "Architecture change", "Why now", "Validation required", "Priority", "Follow-up bundle path", "Status"],
  [
    ["ARCH-01", "Static asset startup failure", "Harden validation host/static asset diagnostics", "Local proof otherwise becomes misleading", "Startup smoke proof", "High", followUpBundle, "Prepared"],
    ["ARCH-02", "Runtime profile override proof", "Expose active DB/profile origin", "Avoid validating wrong storage", "API/UI status proof", "High", followUpBundle, "Prepared"],
    ["ARCH-03", "Transfer preview lacks file payloads", "Add file/data source-truth transfer", "Complete replay needs files and hashes", "Transfer proof with hashes", "High", followUpBundle, "Prepared"],
    ["ARCH-04", "Default consolidation scanned 0", "Add restricted-source warnings and controls", "Silent misses are dangerous", "Run warning proof", "High", followUpBundle, "Prepared"],
    ["ARCH-05", "Candidate budget stopped run", "Add resumable budgets/cursors", "Large source truth needs continuation", "Multi-cycle proof", "High", followUpBundle, "Prepared"],
    ["ARCH-06", "Generic dream aggregates", "Improve aggregate content and quality gates", "Approval requires concrete facts", "Approved/rejected aggregate proof", "High", followUpBundle, "Prepared"],
    ["ARCH-07", "Probe drops policy", "Persist policy on sessions and reuse on turns", "Restricted probes currently invalid", "Restricted/project probe tests", "High", followUpBundle, "Prepared"],
    ["ARCH-08", "Probe omits vector options", "Pass projection options through probe ask", "Qdrant probe validation missing", "Probe trace with Qdrant stage", "Medium", followUpBundle, "Prepared"],
    ["ARCH-09", "Explicit Qdrant options required", "Add projection diagnostics/defaults", "Operator trust requires clear vector status", "Projection/recall proof", "Medium", followUpBundle, "Prepared"],
    ["ARCH-10", "No unattended cycle ledger", "Add long-run orchestration", "User requested longer-term observation", "Completed cycle workbook", "High", followUpBundle, "Prepared"],
  ],
);

const summary = await workbook.inspect({
  kind: "table",
  range: "Summary!A1:F10",
  include: "values,formulas",
  tableMaxRows: 12,
  tableMaxCols: 6,
});
console.log(summary.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "formula error scan",
});
console.log(errors.ndjson);

for (const sheetName of ["Summary", "Checklist", "Environment", "TransferIngestion", "DreamingProbes", "ClusterSearchUI", "Troubles", "FollowUpArchitecture"]) {
  await workbook.render({ sheetName, range: "A1:H14", scale: 1 });
}

await fs.mkdir(path.dirname(outputPath), { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`saved:${outputPath}`);
