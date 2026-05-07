import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const repoRoot = "C:/repositories/CanDoItAll";
const outputPath = path.join(repoRoot, ".codex/bundles/api-swagger-jwt-dev-control-plane/requirements/user-stories.xlsx");

const stories = [
  ["US-001", "API consumer", "Discover API schema", "View Swagger/OpenAPI metadata for projects, processes, agents, and project structure routes.", "R-001", "01", "Implemented"],
  ["US-002", "Developer", "Run without auth", "Start the app from default appsettings with JWT disabled and call development API locally without bearer token.", "R-002", "01", "Implemented"],
  ["US-003", "Developer", "Enable JWT", "Turn on JWT in appsettings and require bearer tokens for API groups.", "R-002,R-003", "01", "Implemented"],
  ["US-004", "Developer", "Create API token", "Open Settings and create a signed bearer token when JWT is active.", "R-004", "03", "Implemented"],
  ["US-005", "API consumer", "Manage projects", "List, inspect, save, delete, and manage hierarchy links through service-backed endpoints.", "R-005", "02", "Implemented"],
  ["US-006", "API consumer", "Use project structure", "Read and mutate project-structure nodes, assets, leases, imports, checklists, dependencies, and analytics.", "R-006", "02", "Implemented"],
  ["US-007", "API consumer", "Edit process definitions", "Load, save, publish, delete, import, and export process definitions.", "R-007", "02", "Implemented"],
  ["US-008", "API consumer", "Control runs", "Start runs, transition steps, rerun agent steps, resolve assignments, record artifacts, and stop blocked runs.", "R-007", "02", "Implemented"],
  ["US-009", "Process operator", "Message process manager", "Send manager directives and role-to-role direct messages for a process run.", "R-007", "02", "Implemented"],
  ["US-010", "Process operator", "Filter run detail", "Request only artifacts, assignments, or step-specific details needed for a focused test.", "R-008", "02", "Implemented"],
  ["US-011", "Project-structure operator", "Launch process from project node", "Create launch plan with project-structure context and execute the full project flow.", "R-007", "02", "Implemented"],
  ["US-012", "HR/process operator", "Match resources", "Run HR matching for launch-plan roles and inspect selected candidates.", "R-007", "02", "Implemented"],
  ["US-013", "Agent operator", "Manage agents", "List, inspect, save, delete, clone, chat with, and inspect execution history for agents.", "R-009", "02", "Implemented"],
  ["US-014", "Reviewer", "Check coverage", "Use this workbook to verify every raw note maps to implemented routes and proof.", "R-010", "04", "Implemented"],
  ["US-015", "Architect", "Review direction", "Confirm API handlers reuse existing services and add repair work before continuing if they drift.", "R-011", "04", "Implemented"]
];

const routes = [
  ["Projects", "GET /api/dev/projects", "ProjectsService.ListAsync", "Project list with summaries"],
  ["Projects", "GET /api/dev/projects/{projectId}", "ProjectsService.GetAsync", "Project editor payload"],
  ["Projects", "POST /api/dev/projects", "ProjectsService.SaveAsync", "Create/update project"],
  ["Projects", "DELETE /api/dev/projects/{projectId}", "ProjectsService.DeleteAsync", "Delete project"],
  ["Projects", "GET /api/dev/projects/{projectId}/hierarchy", "ProjectsService.GetHierarchyAsync", "Hierarchy snapshot"],
  ["Project structure", "/api/project-structure-mcp/*", "ProjectStructureAgentApi", "Existing rich structure API"],
  ["Processes", "GET /api/dev/processes/definitions", "ProcessesService.ListDefinitionsAsync", "Definition list"],
  ["Processes", "GET /api/dev/processes/definitions/editor", "ProcessesService.GetEditorAsync", "Editor model"],
  ["Processes", "POST /api/dev/processes/definitions", "ProcessesService.SaveAsync", "Save definition"],
  ["Processes", "POST /api/dev/processes/definitions/{definitionId}/publish", "ProcessesService.PublishAsync", "Publish definition"],
  ["Processes", "GET /api/dev/processes/runs", "ProcessesService.ListRunsAsync", "Run list"],
  ["Processes", "GET /api/dev/processes/runs/{runId}", "ProcessesService run detail reads", "Filtered run detail"],
  ["Processes", "POST /api/dev/processes/runs", "ProcessesService.StartRunAsync", "Start run"],
  ["Processes", "POST /api/dev/processes/steps/transition", "ProcessesService.TransitionStepAsync", "Step transition"],
  ["Processes", "POST /api/dev/processes/steps/rerun-agent", "ProcessesService.RerunAgentStepAsync", "Agent step rerun"],
  ["Processes", "POST /api/dev/processes/assignments/resolve", "ProcessesService.ResolveAssignmentAsync", "Assignment resolution"],
  ["Processes", "POST /api/dev/processes/artifacts", "ProcessesService.RecordArtifactAsync", "Artifact record"],
  ["Processes", "POST /api/dev/processes/direct-messages", "ProcessesService.SendDirectMessageAsync", "Role direct message"],
  ["Processes", "POST /api/dev/processes/manager-directives", "ProcessesService.RecordManagerDirectiveAsync", "Manager directive"],
  ["Launch plans", "POST /api/dev/processes/launch-plans", "ProcessesService.CreateLaunchPlanAsync", "Create launch plan"],
  ["Launch plans", "POST /api/dev/processes/launch-plans/{launchPlanId}/hr-match", "ProcessesService.MatchLaunchPlanWithHrManagerAsync", "HR matching"],
  ["Launch plans", "POST /api/dev/processes/launch-plans/{launchPlanId}/execute", "ProcessesService.ExecuteLaunchPlanAsync", "Execute plan"],
  ["Agents", "GET /api/dev/agents", "IAgentFrameworkWorkspaceService.ListAgentsAsync", "Agent list"],
  ["Agents", "POST /api/dev/agents", "IAgentFrameworkWorkspaceService.SaveAgentAsync", "Save agent"],
  ["Agents", "POST /api/dev/agents/{agentId}/chat", "IAgentFrameworkWorkspaceService.SendMessageAsync", "Chat"],
  ["Agents", "GET /api/dev/agents/execution-runs", "IAgentFrameworkWorkspaceService.ListExecutionRunsAsync", "Run history"]
];

const workbook = Workbook.create();
const summary = workbook.worksheets.add("Summary");
const storySheet = workbook.worksheets.add("User Stories");
const routeSheet = workbook.worksheets.add("Route Coverage");

summary.getRange("A1:D1").values = [["Metric", "Value", "Target", "Status"]];
summary.getRange("A2:D5").values = [
  ["Story count", stories.length, "Every raw note mapped", "Implemented"],
  ["Route groups", 4, "Projects, project structure, processes, agents", "Implemented"],
  ["Critical foundations", 2, "Auth/OpenAPI and API surface", "Implemented"],
  ["JWT default", "Disabled", "App starts without token config", "Implemented"]
];

storySheet.getRange("A1:G1").values = [["Story Id", "Actor", "Goal", "Acceptance", "Requirements", "Subbundle", "Status"]];
storySheet.getRangeByIndexes(1, 0, stories.length, 7).values = stories;

routeSheet.getRange("A1:D1").values = [["Area", "Route", "Reused Service", "Coverage"]];
routeSheet.getRangeByIndexes(1, 0, routes.length, 4).values = routes;

for (const sheet of [summary, storySheet, routeSheet]) {
  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);
  const used = sheet.getUsedRange();
  used.format.wrapText = true;
  sheet.getRange("A1:Z1").format = {
    fill: "#0F766E",
    font: { bold: true, color: "#FFFFFF" }
  };
}

summary.getRange("A:D").format.columnWidthPx = 190;
storySheet.getRange("A:A").format.columnWidthPx = 90;
storySheet.getRange("B:B").format.columnWidthPx = 150;
storySheet.getRange("C:C").format.columnWidthPx = 180;
storySheet.getRange("D:D").format.columnWidthPx = 460;
storySheet.getRange("E:G").format.columnWidthPx = 120;
routeSheet.getRange("A:A").format.columnWidthPx = 150;
routeSheet.getRange("B:B").format.columnWidthPx = 300;
routeSheet.getRange("C:C").format.columnWidthPx = 360;
routeSheet.getRange("D:D").format.columnWidthPx = 260;

storySheet.tables.add(`A1:G${stories.length + 1}`, true, "UserStoriesTable");
routeSheet.tables.add(`A1:D${routes.length + 1}`, true, "RouteCoverageTable");

const inspect = await workbook.inspect({
  kind: "table",
  range: "User Stories!A1:G16",
  include: "values",
  tableMaxRows: 16,
  tableMaxCols: 7
});
console.log(inspect.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
  summary: "formula error scan"
});
console.log(errors.ndjson);

await fs.mkdir(path.dirname(outputPath), { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(outputPath);
