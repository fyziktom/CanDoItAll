import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const repoRoot = path.resolve(process.cwd(), "../../..");
const bundleRoot = path.join(repoRoot, "codex", "bundles", "api-docs-skills-parity-v1");
const outputDir = path.join(bundleRoot, "inventories");
const outputXlsx = path.join(outputDir, "api-docs-skills-gap-map.xlsx");
const previewPng = path.join(outputDir, "api-docs-skills-gap-map-summary.png");
const inspectJson = path.join(outputDir, "api-docs-skills-gap-map-inspect.json");

const apiDir = path.join(repoRoot, "src", "CanDoItAll.Web", "Api");
const projectStructureApi = path.join(repoRoot, "src", "CanDoItAll.Web", "ProjectStructureAgentApi.cs");
const docsDir = path.join(repoRoot, "docs");
const skillsDir = path.join(repoRoot, "codex", "skills");

const sourceSnapshotId = "snap-20260530233954-854cccd0";
const generatedUtc = `UTC ${new Date().toISOString()}`;

const mapMethodPattern = /(\w+)\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]*)"/;
const mapGroupPattern = /var\s+(\w+)\s*=\s*(\w+|endpoints)\.MapGroup\(\s*"([^"]*)"\s*\)/;
const withNamePattern = /\.WithName\(\s*"([^"]+)"/;

async function listFiles(root, predicate) {
  const entries = await fs.readdir(root, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = path.join(root, entry.name);
    if (entry.isDirectory()) {
      files.push(...await listFiles(fullPath, predicate));
      continue;
    }

    if (!predicate || predicate(fullPath)) {
      files.push(fullPath);
    }
  }

  return files;
}

function normalizeRoute(route) {
  if (!route) {
    return "/";
  }

  const withSlash = route.startsWith("/") ? route : `/${route}`;
  return withSlash.length > 1
    ? withSlash.replace(/\/+/g, "/").replace(/\/$/, "")
    : withSlash;
}

function combineRoute(basePath, childPath) {
  const base = normalizeRoute(basePath);
  const child = childPath === "/" ? "" : normalizeRoute(childPath);
  return normalizeRoute(`${base}${child}`);
}

function canonicalRoute(route) {
  return normalizeRoute(route)
    .replace(/:guid/g, "")
    .replace(/:int/g, "")
    .replace(/:long/g, "")
    .replace(/:bool/g, "")
    .replace(/:double/g, "");
}

function inferSurface(route) {
  const normalized = normalizeRoute(route);
  if (normalized.startsWith("/api/cognitive-memory/v1")) {
    return "cognitive-memory-v1";
  }

  const match = normalized.match(/^\/api\/([^/]+)/);
  return match ? match[1] : "other";
}

function findEndpointName(lines, startIndex) {
  for (let offset = 0; offset < 12 && startIndex + offset < lines.length; offset += 1) {
    const match = lines[startIndex + offset].match(withNamePattern);
    if (match) {
      return match[1];
    }
  }

  return "";
}

async function parseApiEndpoints() {
  const apiFiles = await listFiles(apiDir, file => file.endsWith(".cs"));
  apiFiles.push(projectStructureApi);

  const endpoints = [];

  for (const file of apiFiles.sort()) {
    const relativeFile = path.relative(repoRoot, file).replaceAll("\\", "/");
    const content = await fs.readFile(file, "utf8");
    const lines = content.split(/\r?\n/);
    const fileName = path.basename(file);
    const isCognitiveMemoryPartial = fileName.startsWith("CognitiveMemoryApi.") && fileName !== "CognitiveMemoryApi.cs";

    if (isCognitiveMemoryPartial) {
      for (let index = 0; index < lines.length; index += 1) {
        const match = lines[index].match(mapMethodPattern);
        if (!match || match[1] !== "memory") {
          continue;
        }

        const [, , methodSuffix, routeTemplate] = match;
        for (const basePath of ["/api/cognitive-memory", "/api/cognitive-memory/v1"]) {
          const route = combineRoute(basePath, routeTemplate);
          endpoints.push({
            Method: methodSuffix.toUpperCase(),
            Route: route,
            CanonicalRoute: canonicalRoute(route),
            Surface: inferSurface(route),
            SourceFile: relativeFile,
            Line: index + 1,
            EndpointName: findEndpointName(lines, index)
          });
        }
      }

      continue;
    }

    const groups = new Map();
    groups.set("endpoints", "");
    groups.set("group", relativeFile === "src/CanDoItAll.Web/ProjectStructureAgentApi.cs" ? "" : "/api");

    for (let index = 0; index < lines.length; index += 1) {
      const groupMatch = lines[index].match(mapGroupPattern);
      if (groupMatch) {
        const [, variableName, parentName, routeTemplate] = groupMatch;
        const parentPath = groups.get(parentName) ?? "";
        groups.set(variableName, combineRoute(parentPath, routeTemplate));
      }

      const endpointMatch = lines[index].match(mapMethodPattern);
      if (!endpointMatch) {
        continue;
      }

      const [, variableName, methodSuffix, routeTemplate] = endpointMatch;
      if (!groups.has(variableName)) {
        continue;
      }

      const route = combineRoute(groups.get(variableName), routeTemplate);
      endpoints.push({
        Method: methodSuffix.toUpperCase(),
        Route: route,
        CanonicalRoute: canonicalRoute(route),
        Surface: inferSurface(route),
        SourceFile: relativeFile,
        Line: index + 1,
        EndpointName: findEndpointName(lines, index)
      });
    }
  }

  return endpoints
    .filter(endpoint => endpoint.Route.startsWith("/api/"))
    .sort((left, right) =>
      left.Surface.localeCompare(right.Surface)
      || left.Route.localeCompare(right.Route)
      || left.Method.localeCompare(right.Method));
}

async function readCorpus(root) {
  const files = await listFiles(root, file => file.endsWith(".md") || file.endsWith(".cs"));
  const parts = [];

  for (const file of files) {
    const text = await fs.readFile(file, "utf8");
    parts.push(canonicalRoute(text));
  }

  return parts.join("\n").toLowerCase();
}

function containsRoute(corpus, route) {
  return corpus.includes(canonicalRoute(route).toLowerCase());
}

function countBy(items, selector) {
  const counts = new Map();
  for (const item of items) {
    const key = selector(item);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }

  return [...counts.entries()].sort(([left], [right]) => left.localeCompare(right));
}

function asRows(headers, records) {
  return [
    headers,
    ...records.map(record => headers.map(header => record[header] ?? ""))
  ];
}

function formatSheet(sheet, rowCount, colCount) {
  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);
  const header = sheet.getRangeByIndexes(0, 0, 1, colCount);
  header.format = {
    fill: "#1F2937",
    font: { bold: true, color: "#FFFFFF" },
    wrapText: true
  };

  const used = sheet.getRangeByIndexes(0, 0, rowCount, colCount);
  used.format.wrapText = true;
  used.format.autofitColumns();
  used.format.autofitRows();
}

function addSheet(workbook, name, rows) {
  const sheet = workbook.worksheets.add(name);
  sheet.getRangeByIndexes(0, 0, rows.length, rows[0].length).values = rows;
  formatSheet(sheet, rows.length, rows[0].length);
  return sheet;
}

function priorityRank(priority) {
  return {
    Critical: 0,
    High: 1,
    Medium: 2,
    Low: 3
  }[priority] ?? 99;
}

function buildGapRows() {
  const rows = [
    {
      ID: "GAP-001",
      Area: "Inventory",
      Priority: "Critical",
      Finding: "No durable source-of-truth route and DTO gap map exists for the current control-plane APIs.",
      Evidence: "Source inventory now regenerates 271 non-alias HTTP routes and 309 routes including Cognitive Memory v1 aliases.",
      Repair: "Keep this workbook and generated route inventory as the first artifact in the repair workflow.",
      Subbundle: "SB01",
      Validation: "Regenerate XLSX from source and review Summary/API Inventory sheets before implementation."
    },
    {
      ID: "GAP-002",
      Area: "Cognitive Memory docs",
      Priority: "Critical",
      Finding: "The Cognitive Memory operations API doc says 35 routes per surface, but source exposes 38 routes per surface.",
      Evidence: "src/CanDoItAll.Web/Api/CognitiveMemoryApi.ContractEndpoints.cs line 155 and partial endpoint files.",
      Repair: "Update route count, route table, and examples for /contract, /projections/rebuild, /automation/run, and /retention/cleanup.",
      Subbundle: "SB04",
      Validation: "Docs route list must match the generated contract route list for both legacy and v1 surfaces."
    },
    {
      ID: "GAP-003",
      Area: "API tests",
      Priority: "Critical",
      Finding: "Focused OpenAPI integration coverage omits Cognitive Memory contract and operations routes, and does not assert v1 aliases.",
      Evidence: "tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs Api_openapi_exposes_focused_control_plane_routes.",
      Repair: "Add assertions for /api/cognitive-memory/contract, /projections/rebuild, /automation/run, /retention/cleanup, and v1 equivalents.",
      Subbundle: "SB02",
      Validation: "Run focused ApiIntegrationTests after the assertions are added."
    },
    {
      ID: "GAP-004",
      Area: "API control-plane docs",
      Priority: "High",
      Finding: "The API control-plane doc points developers to Project Structure, Processes, and Agents skills but omits Workflows and Cognitive Memory.",
      Evidence: "docs/api-control-plane.md development workflow section.",
      Repair: "Update the skill list and include a surface-to-skill map covering agents, workflows, processes, project-structure, cognitive-memory, and plugin/project decision points.",
      Subbundle: "SB04",
      Validation: "Markdown review plus route/surface map cross-check."
    },
    {
      ID: "GAP-005",
      Area: "Agents skill",
      Priority: "High",
      Finding: "The Agents API skill is too high-level for current teams, execution-run filters, approvals, metrics, and runtime snapshot calls.",
      Evidence: "codex/skills/candoitall-api-agents/SKILL.md versus src/CanDoItAll.Web/Api/AgentsApi.cs.",
      Repair: "Add exact route tables, request/query DTO fields, and client workflow examples for teams/providers/capabilities/chat/execution runs.",
      Subbundle: "SB05",
      Validation: "Skill route table must contain all generated /api/agents routes or explicitly mark intentional omissions."
    },
    {
      ID: "GAP-006",
      Area: "Workflows skill",
      Priority: "High",
      Finding: "The Workflows API skill lacks precise DTO details for paged run/event queries, start-source fields, and artifact content access.",
      Evidence: "WorkflowRunStartApiRequest, WorkflowRunListApiQuery, WorkflowEventListApiQuery, and WorkflowsApi.cs route inventory.",
      Repair: "Add DTO tables and route examples for source process linkage, pagination, pending requests, artifacts, analytics, and validation.",
      Subbundle: "SB05",
      Validation: "Route and DTO table review against WorkflowsApi.cs and WorkflowApiIntegrationTests."
    },
    {
      ID: "GAP-007",
      Area: "Processes skill",
      Priority: "High",
      Finding: "The Processes API skill is rich but still narrative-heavy for a 58-route surface, making client implementation prone to missed nested routes.",
      Evidence: "src/CanDoItAll.Web/Api/ProcessesApi.cs and codex/skills/candoitall-api-processes/SKILL.md.",
      Repair: "Add exact generated route appendix, DTO field map, live-run freshness/profile fields, and route-level validation notes.",
      Subbundle: "SB05",
      Validation: "Process skill route appendix matches source and focused process API tests."
    },
    {
      ID: "GAP-008",
      Area: "Project Structure skill",
      Priority: "Critical",
      Finding: "The Project Structure API skill is under-specified for the 51-route surface and omits several node, workflow, asset, and lease operations.",
      Evidence: "src/CanDoItAll.Web/ProjectStructureAgentApi.cs versus codex/skills/candoitall-api-project-structure/SKILL.md.",
      Repair: "Add route table and DTO guidance for metadata/status/progress/markers/priority, command/process/workflow, asset content/revisions, lease renew/current, links, dependencies, and analytics.",
      Subbundle: "SB05",
      Validation: "Skill route table must match generated /api/project-structure routes."
    },
    {
      ID: "GAP-009",
      Area: "Cognitive Memory skill",
      Priority: "Critical",
      Finding: "The Cognitive Memory API skill omits exact v1 alias/contract/database-transfer coverage and compact DTO maps for advanced operations.",
      Evidence: "CognitiveMemoryApi.*Endpoints.cs and CognitiveMemoryApiDtos.cs versus codex/skills/candoitall-api-cognitive-memory/SKILL.md.",
      Repair: "Update the skill with both base paths, 38-route contract, database transfer, settings, projection, automation, retention, recall/review, professor, answer gate, and distributed worker DTO groups.",
      Subbundle: "SB05",
      Validation: "Skill examples should use /api/cognitive-memory/v1 for new integrations and mention legacy compatibility."
    },
    {
      ID: "GAP-010",
      Area: "Agent process tools",
      Priority: "Critical",
      Finding: "Internal MAF process tools expose 23 tools versus 58 process HTTP routes; orchestration agents cannot perform many supported operations without HTTP fallback.",
      Evidence: "src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs and ProcessesApi.cs.",
      Repair: "Either add missing tools for launch plans, escalations, approvals, direct messages, manager directives, live templates/profiles, scoped artifacts/assignments, and run recovery, or explicitly document HTTP-only operations.",
      Subbundle: "SB03",
      Validation: "Tool policy constants, runtime tool descriptors, and tests are updated together."
    },
    {
      ID: "GAP-011",
      Area: "Agent project-structure tools",
      Priority: "Critical",
      Finding: "Internal Project Structure tools expose 28 tools versus 51 HTTP routes; node metadata/status/progress/marker/priority and workflow/process operations are missing.",
      Evidence: "src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs and ProjectStructureAgentApi.cs.",
      Repair: "Add missing strongly typed tools or define intentional HTTP skill fallback with clear capability naming.",
      Subbundle: "SB03",
      Validation: "Tool policy, runtime descriptors, and capability/approval tests reflect each supported tool."
    },
    {
      ID: "GAP-012",
      Area: "Providers docs and skills",
      Priority: "High",
      Finding: "Provider documentation and skills do not capture private provider pricing, tags, native hosted tool families, local MCP, image generation, structured output, and reasoning-effort policy.",
      Evidence: "ProviderFeatureMatrix, ProviderProfile, ProviderPricingDefaults, AgentProviderModelParameterPolicy.",
      Repair: "Add provider capability matrix and model-parameter guidance to docs and Agents skill.",
      Subbundle: "SB04/SB05",
      Validation: "Docs list all current ProviderFeatureMatrix flags and parameter policy cases."
    },
    {
      ID: "GAP-013",
      Area: "DTO enum guidance",
      Priority: "Medium",
      Finding: "Enum serialization guidance exists in places but lacks a generated enum map and validation examples for API clients.",
      Evidence: "Process skill has warnings, but DTO field maps across agents/workflows/cognitive-memory are incomplete.",
      Repair: "Add enum/value tables for request DTOs where clients pass strings, especially cognitive memory policy and process/workflow states.",
      Subbundle: "SB05/SB06",
      Validation: "API client examples use exact enum strings accepted by source parsers."
    },
    {
      ID: "GAP-014",
      Area: "Historical docs",
      Priority: "Medium",
      Finding: "Historical proof docs can be mistaken for current status because they lack explicit superseded/historical framing.",
      Evidence: "docs/agent-runtime-hardening-verification.md is dated 2026-04-27.",
      Repair: "Add a historical banner and link to the new parity bundle and current validation commands.",
      Subbundle: "SB04",
      Validation: "Docs clearly distinguish proof records from living API guidance."
    },
    {
      ID: "GAP-015",
      Area: "Drift guardrails",
      Priority: "Critical",
      Finding: "No guardrail fails when route/source changes without docs, skills, and workbook updates.",
      Evidence: "Current route drift was found by manual analysis rather than a repo test.",
      Repair: "Add generated inventory checks or focused tests that compare source routes against OpenAPI, docs, and skills coverage expectations.",
      Subbundle: "SB06",
      Validation: "A focused test or script fails on missing high-priority route coverage."
    },
    {
      ID: "GAP-016",
      Area: "Skills coverage decision",
      Priority: "Medium",
      Finding: "Plugins and Projects expose HTTP APIs but have no dedicated repo-managed API skill.",
      Evidence: "Generated inventory contains /api/plugins and /api/projects routes; codex/skills has no matching API skill.",
      Repair: "Decide whether to create plugin/project API skills or explicitly include them in the general control-plane docs with no separate skill.",
      Subbundle: "SB05",
      Validation: "Decision is recorded and reflected in docs/skill index."
    }
  ];
  const closures = new Map([
    ["GAP-001", "Workbook and preserved builder regenerate source route, DTO, docs, skills, and tool parity sheets."],
    ["GAP-002", "Cognitive Memory API doc now states 38 routes per surface and includes transfer, contract, projection, automation, and retention routes."],
    ["GAP-003", "Focused OpenAPI route test now asserts missing Cognitive Memory legacy and v1 routes."],
    ["GAP-004", "API control-plane doc now lists Workflows and Cognitive Memory skills."],
    ["GAP-005", "Agents skill now includes teams, provider capability/pricing/model-parameter guidance, DTO fields, and a generated 57-route appendix."],
    ["GAP-006", "Workflows skill now includes runtime DTO fields, artifact content route, and a generated 37-route appendix."],
    ["GAP-007", "Processes skill now includes run detail DTO fields, HTTP-only tool boundary, artifact lineage guidance, and a generated 58-route appendix."],
    ["GAP-008", "Project Structure skill now includes missing route families, HTTP-only tool boundary, and a generated 51-route appendix."],
    ["GAP-009", "Cognitive Memory skill now includes v1, contract, transfer, operation DTOs, and generated legacy/v1 route appendices."],
    ["GAP-010", "Resolved as explicit HTTP-only boundary in docs and skills; no broad runtime tool expansion was introduced."],
    ["GAP-011", "Resolved as explicit HTTP-only boundary in docs and skills; no broad runtime tool expansion was introduced."],
    ["GAP-012", "Provider capability/pricing/model-parameter guidance added to docs and Agents skill."],
    ["GAP-013", "DTO and enum-sensitive field guidance added to skills/docs for high-risk surfaces."],
    ["GAP-014", "Historical runtime proof doc now has an explicit historical banner."],
    ["GAP-015", "ApiDocsSkillsParityTests guard critical route/docs/skills coverage."],
    ["GAP-016", "Decision recorded in API control-plane docs: Projects and Plugins remain OpenAPI/source-driven without dedicated skills."]
  ]);

  return rows
    .map(row => ({
      Status: "Closed",
      Closure: closures.get(row.ID) ?? "Closed by bundle execution.",
      ...row
    }))
    .sort((left, right) => priorityRank(left.Priority) - priorityRank(right.Priority) || left.ID.localeCompare(right.ID));
}

function buildDtoRows() {
  return [
    {
      Surface: "agents",
      DTO: "AgentExecutionRunStartApiRequest",
      Fields: "Prompt, ChatSessionId, Context, AutoApprovePendingToolCalls, StructuredOutput",
      Source: "src/CanDoItAll.Web/Api/AgentsApi.cs",
      Gap: "Skill needs exact execution-run start payload and structured-output guidance.",
      Subbundle: "SB05"
    },
    {
      Surface: "agents",
      DTO: "AgentExecutionRunApiQuery",
      Fields: "AgentId, ChatSessionId, CorrelationId, SourceKind, SourceId, Take, ProcessRunId, ProcessStepId, SchedulerRunId, MessageId, State, Outcome, ApprovalStatus, CreatedFrom/ToUtc, UpdatedFrom/ToUtc",
      Source: "src/CanDoItAll.Web/Api/AgentsApi.cs",
      Gap: "Skill should document all filters and date semantics.",
      Subbundle: "SB05"
    },
    {
      Surface: "workflows",
      DTO: "WorkflowRunStartApiRequest",
      Fields: "WorkflowId, VersionId, InputJson, RequestedBackend, SourceProcessRunId, SourceProcessAssignmentId",
      Source: "src/CanDoItAll.Web/Api/WorkflowsApi.cs",
      Gap: "Skill needs process-source fields and backend selection behavior.",
      Subbundle: "SB05"
    },
    {
      Surface: "workflows",
      DTO: "WorkflowRunListApiQuery / WorkflowEventListApiQuery",
      Fields: "WorkflowId, State, Backend, Search, Take, PageIndex, PageSize; PageIndex, PageSize",
      Source: "src/CanDoItAll.Web/Api/WorkflowsApi.cs",
      Gap: "Skill and docs need paging/filter tables for run and event list endpoints.",
      Subbundle: "SB05"
    },
    {
      Surface: "processes",
      DTO: "ProcessRunListApiQuery",
      Fields: "DefinitionId, ProjectId, Status, OperatingMode, Search, Take",
      Source: "src/CanDoItAll.Web/Api/ProcessesApi.cs",
      Gap: "Skill should keep a compact DTO appendix for run list filters.",
      Subbundle: "SB05"
    },
    {
      Surface: "processes",
      DTO: "ProcessRunDetailApiQuery",
      Fields: "StepRunId, StepDefinitionId, RoleRequirementId, PartyId, ArtifactId, ArtifactExpectationId, AgentId, WorkflowRunId, WorkflowDefinitionId, WorkflowVersionId, StepStatus, ArtifactKind, ExecutionState, WorkflowState, Search, Take, IncludeDecisions, IncludeArtifacts, IncludeOutboxRecords, IncludeAssignments, IncludeWorkBriefs, IncludeConformanceObservations, IncludeDirectMessages, IncludeExecutionRuns, IncludeWorkflowRuns, IncludeEscalations, IncludeOperatorApprovals, IncludeAttemptTimeline",
      Source: "src/CanDoItAll.Web/Api/ProcessesApi.cs",
      Gap: "Skill/docs need exact detail query shape because the endpoint is the main operator surface.",
      Subbundle: "SB05"
    },
    {
      Surface: "processes",
      DTO: "ProcessArtifactRecordApiRequest",
      Fields: "StepRunId, ArtifactExpectationId, ArtifactKind, DisplayName, Summary, Content, ContentPath, ExternalReferenceKey, ProjectionLineage",
      Source: "src/CanDoItAll.Web/Api/ProcessesApi.cs",
      Gap: "Docs need projection lineage and external reference guidance.",
      Subbundle: "SB04/SB05"
    },
    {
      Surface: "cognitive-memory",
      DTO: "CognitiveMemorySettingsApiRequest",
      Fields: "IsEnabled, ScheduleMode, NightlyLocalTime, IdleMinutes, ScheduledLocalTimes, AutoIngestProjectStructure, AutoIngestProcessRuntime, AutoConsolidateAfterIngestion, ModelAccessMode, DefaultProviderProfileId, DefaultAgentId, AllowedProviderProfileIds, ModelExecutionProfiles, ActorId",
      Source: "src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs",
      Gap: "Skill/docs need settings policy and profile field explanations.",
      Subbundle: "SB04/SB05"
    },
    {
      Surface: "cognitive-memory",
      DTO: "CognitiveMemoryProjectionRebuildApiRequest",
      Fields: "ProjectId, Take, ActorId, CollectionName, ProjectMissingRecords, ProjectionProfileId, EmbeddingProfileId, TargetProviderName, ProjectionStoreKind, VectorDimensions",
      Source: "src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs",
      Gap: "Missing or stale docs for projection rebuild route and projection profile fields.",
      Subbundle: "SB04/SB05"
    },
    {
      Surface: "cognitive-memory",
      DTO: "CognitiveMemoryAutomationRunApiRequest",
      Fields: "ProjectId, TriggerKind, ActorId, Take, CycleId, MaxCycles, ContinueUntilIdle, Policy",
      Source: "src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs",
      Gap: "Docs/skill need automation execution and policy budget semantics.",
      Subbundle: "SB04/SB05"
    },
    {
      Surface: "cognitive-memory",
      DTO: "CognitiveMemoryRetentionCleanupApiRequest",
      Fields: "ProjectId, DeleteBeforeUtc, DryRun, Scopes, ActorId",
      Source: "src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs",
      Gap: "Docs/skill must call out DryRun default and retention scopes.",
      Subbundle: "SB04/SB05"
    },
    {
      Surface: "providers",
      DTO: "ProviderFeatureMatrix / ProviderProfile",
      Fields: "SupportsStreaming, SupportsTools, SupportsStructuredOutput, SupportsToolApprovals, SupportsBackgroundResponses, SupportsNativeCodeInterpreter, SupportsNativeFileSearch, SupportsNativeWebSearch, SupportsNativeHostedMcp, SupportsLocalMcp, SupportsVision, SupportsCompaction, SupportsImageGeneration, IsPrivateProvider, ModelPrices, Tags",
      Source: "src/CanDoItAll.AgentFramework.Models and provider services",
      Gap: "Docs and Agents skill need provider feature/pricing/tag matrix.",
      Subbundle: "SB04/SB05"
    }
  ];
}

function buildDocsSkillRows() {
  return [
    {
      Artifact: "docs/api-control-plane.md",
      Type: "Doc",
      Status: "Needs update",
      Gap: "Omit Workflows and Cognitive Memory skills from the development workflow skill list.",
      Repair: "Add surface-to-skill map and plugin/project decision."
    },
    {
      Artifact: "docs/cognitive-memory/operations/api.md",
      Type: "Doc",
      Status: "Needs update",
      Gap: "States 35 routes per surface while source exposes 38; needs contract/projection/automation/retention coverage.",
      Repair: "Regenerate route table from CognitiveMemory contract/source."
    },
    {
      Artifact: "docs/process-agent-operator-runbook.md",
      Type: "Doc",
      Status: "Needs update",
      Gap: "Needs current run/detail DTO, recoveryOptions, freshness/profile, projection-lineage and diagnostics coverage.",
      Repair: "Refresh with exact API usage and validation commands."
    },
    {
      Artifact: "docs/agent-runtime-hardening-verification.md",
      Type: "Doc",
      Status: "Historical",
      Gap: "Dated proof record can be read as current guidance.",
      Repair: "Add historical/superseded framing and link to current bundle."
    },
    {
      Artifact: "codex/skills/candoitall-api-agents/SKILL.md",
      Type: "Skill",
      Status: "Needs update",
      Gap: "Route table and DTO details incomplete for current agents/provider/capability/runtime surface.",
      Repair: "Add exact route/DTO appendix and examples."
    },
    {
      Artifact: "codex/skills/candoitall-api-workflows/SKILL.md",
      Type: "Skill",
      Status: "Needs update",
      Gap: "Missing precise paging/source/artifact DTO details.",
      Repair: "Add route/DTO appendix."
    },
    {
      Artifact: "codex/skills/candoitall-api-processes/SKILL.md",
      Type: "Skill",
      Status: "Needs update",
      Gap: "Rich guidance but route appendix and latest DTO details must be reconciled.",
      Repair: "Add exact generated route list and current DTO map."
    },
    {
      Artifact: "codex/skills/candoitall-api-project-structure/SKILL.md",
      Type: "Skill",
      Status: "Needs update",
      Gap: "Under-specified for the 51-route surface.",
      Repair: "Add route table and DTO/operation groups."
    },
    {
      Artifact: "codex/skills/candoitall-api-cognitive-memory/SKILL.md",
      Type: "Skill",
      Status: "Needs update",
      Gap: "Missing v1/contract/database-transfer and advanced DTO coverage.",
      Repair: "Add current 38-route-per-surface contract and DTO groups."
    },
    {
      Artifact: "Active skill root copies",
      Type: "Skill sync",
      Status: "Currently hash-aligned",
      Gap: "Repo and active root hashes matched before edits; repairs must sync both places after skill edits.",
      Repair: "Copy updated skills to active root and record hash proof."
    }
  ];
}

function buildToolParityRows() {
  return [
    {
      Surface: "processes",
      HttpRoutes: 58,
      RuntimeTools: 23,
      Gap: 35,
      MissingAreas: "launch plans, escalations, operator approvals, manager directives, direct messages, template baseline/live/detail/envelope, scoped artifacts/assignments/artifact detail, recovery controls",
      Source: "MafAgentRuntime.ProcessTools.cs / ProcessesApi.cs",
      Subbundle: "SB03"
    },
    {
      Surface: "project-structure",
      HttpRoutes: 51,
      RuntimeTools: 28,
      Gap: 23,
      MissingAreas: "node metadata/status/progress/markers/priority, command, process-definition/start, workflow add/definition/start/status, asset content, lease renew, route-specific link/unlink variants",
      Source: "MafAgentRuntime.ProjectStructureTools.cs / ProjectStructureAgentApi.cs",
      Subbundle: "SB03"
    }
  ];
}

function buildPlanRows() {
  return [
    {
      Phase: "SB01",
      Name: "Source Of Truth API And DTO Inventory",
      Classification: "Critical foundation",
      Work: "Maintain generated route inventory, DTO map, tool parity map, docs/skills coverage map, and XLSX workbook.",
      Gate: "Workbook and inventory match source before downstream edits start.",
      Validation: "Regenerate XLSX and review Summary/API Inventory/Gaps sheets."
    },
    {
      Phase: "SB02",
      Name: "HTTP API Contract Repairs",
      Classification: "Critical foundation",
      Work: "Repair OpenAPI/contract/test gaps for missing Cognitive Memory contract/operations/v1 routes and any discovered route mismatches.",
      Gate: "Focused API contract tests pass.",
      Validation: "dotnet test --filter Api_openapi_exposes_focused_control_plane_routes"
    },
    {
      Phase: "SB03",
      Name: "Agent Tool Surface Parity",
      Classification: "Critical foundation",
      Work: "Add missing runtime tools or explicitly document HTTP-only operations for processes and project structure.",
      Gate: "Tool policy constants, descriptors, approvals, and tests align.",
      Validation: "Focused unit/integration tests for MafAgentRuntime process/project tools."
    },
    {
      Phase: "SB04",
      Name: "Documentation Refresh",
      Classification: "Documentation",
      Work: "Update API control-plane, Cognitive Memory API, process operator runbook, historical proof docs, and provider capability docs.",
      Gate: "Docs cite current routes/DTO fields and separate historical proof from living guidance.",
      Validation: "git diff --check plus source route assertions."
    },
    {
      Phase: "SB05",
      Name: "API Skills Refresh And Active Skill Sync",
      Classification: "Enablement",
      Work: "Update repo-managed API skills, decide plugin/project skill coverage, sync active skill root, and record hashes.",
      Gate: "Repo and active skill copies match after edits.",
      Validation: "Hash comparison for each edited SKILL.md."
    },
    {
      Phase: "SB06",
      Name: "Validation Harness And Drift Guardrails",
      Classification: "Guardrail",
      Work: "Add or update scripts/tests that fail when source routes drift without docs/skills/test updates.",
      Gate: "Route drift is visible in CI or a documented focused command.",
      Validation: "Run new route/docs/skills parity check."
    },
    {
      Phase: "SB07",
      Name: "Final Closure And Handoff",
      Classification: "Closure",
      Work: "Run validators, capture proof, update execution report, and audit raw request coverage.",
      Gate: "Prepared and completed validators pass, all requirements traced.",
      Validation: "validate_bundle.py --stage completed plus build/focused tests."
    }
  ];
}

function buildValidationRows() {
  return [
    {
      Check: "Prepared bundle validation",
      Command: "python C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-preparation\\scripts\\validate_bundle.py codex\\bundles\\api-docs-skills-parity-v1 --profile initiative --stage prepared",
      Purpose: "Proves the planning bundle is complete enough to execute."
    },
    {
      Check: "Generated workbook",
      Command: "node .codex\\tmp\\api-docs-skills-gap-map\\build-gap-map.mjs",
      Purpose: "Regenerates route inventory, gap map, and preview image from source."
    },
    {
      Check: "Focused OpenAPI route test",
      Command: "dotnet test tests\\CanDoItAll.Tests.Integration\\CanDoItAll.Tests.Integration.csproj --filter Api_openapi_exposes_focused_control_plane_routes",
      Purpose: "Verifies advertised control-plane routes."
    },
    {
      Check: "Process/project tool tests",
      Command: "dotnet test --filter \"MafAgentRuntime|AgentToolInvocationPolicy\"",
      Purpose: "Verifies new tool descriptors and policy coverage after SB03."
    },
    {
      Check: "Docs whitespace/link sanity",
      Command: "git diff --check",
      Purpose: "Catches markdown whitespace and formatting defects."
    },
    {
      Check: "Skill sync proof",
      Command: "Get-FileHash codex\\skills\\candoitall-api-*\\SKILL.md and matching C:\\Users\\lucys\\.codex\\skills copies",
      Purpose: "Proves active skills were updated, not only repo copies."
    }
  ];
}

const endpoints = await parseApiEndpoints();
const docsCorpus = await readCorpus(docsDir);
const skillsCorpus = await readCorpus(skillsDir);

const endpointRows = endpoints.map(endpoint => ({
  Surface: endpoint.Surface,
  Method: endpoint.Method,
  Route: endpoint.Route,
  EndpointName: endpoint.EndpointName,
  SourceFile: endpoint.SourceFile,
  Line: endpoint.Line,
  DocsCoveredByExactRoute: containsRoute(docsCorpus, endpoint.Route) ? "Yes" : "No",
  SkillCoveredByExactRoute: containsRoute(skillsCorpus, endpoint.Route) ? "Yes" : "No"
}));
const focusedControlPlaneCount = endpointRows
  .filter(row => row.Surface !== "access" && row.Surface !== "cognitive-memory-v1")
  .length;
const nonAliasRouteCount = endpointRows
  .filter(row => row.Surface !== "cognitive-memory-v1")
  .length;

const surfaceRows = countBy(endpointRows, row => row.Surface)
  .map(([surface, count]) => {
    const surfaceEndpoints = endpointRows.filter(row => row.Surface === surface);
    return {
      Surface: surface,
      RouteCount: count,
      DocsExactRouteCovered: surfaceEndpoints.filter(row => row.DocsCoveredByExactRoute === "Yes").length,
      SkillsExactRouteCovered: surfaceEndpoints.filter(row => row.SkillCoveredByExactRoute === "Yes").length,
      Notes: surface === "cognitive-memory-v1"
        ? "Alias surface; new integrations should prefer this base path."
        : ""
    };
  });

const gaps = buildGapRows();
const priorityRows = countBy(gaps, row => row.Priority)
  .map(([priority, count]) => ({ Priority: priority, Count: count }))
  .sort((left, right) => priorityRank(left.Priority) - priorityRank(right.Priority));

const workbook = Workbook.create();

const summaryRows = [
  ["Metric", "Value", "Notes"],
  ["Generated UTC", generatedUtc, "Workbook produced by .codex/tmp/api-docs-skills-gap-map/build-gap-map.mjs"],
  ["CodeAnalytics snapshot", sourceSnapshotId, "Scoped source inventory: 10 projects, 728 documents, no blocking errors."],
  ["Total generated /api routes", endpointRows.length, "Includes API access routes and Cognitive Memory legacy+v1 surfaces."],
  ["Non-alias /api route count", nonAliasRouteCount, "Includes the two /api/access routes."],
  ["Focused control-plane route count", focusedControlPlaneCount, "Excludes /api/access and Cognitive Memory v1 aliases."],
  ["Gap findings", gaps.length, "Critical and high-priority findings must be closed before final handoff."],
  ["Critical gaps", gaps.filter(row => row.Priority === "Critical").length, "Inventory, API tests, project/cognitive skills, tool parity, and drift guardrails."],
  ["High gaps", gaps.filter(row => row.Priority === "High").length, "Docs/skills/provider DTO repairs."],
  ["Prepared bundle", "codex/bundles/api-docs-skills-parity-v1", "Execution plan is split into seven subbundles."]
];

const summarySheet = addSheet(workbook, "Summary", summaryRows);
summarySheet.getRange("A1:C1").format.fill = "#0F766E";
summarySheet.getRange("A2:A9").format.font = { bold: true };
summarySheet.getRange("A1:A10").format.columnWidthPx = 260;
summarySheet.getRange("B1:B10").format.columnWidthPx = 260;
summarySheet.getRange("C1:C10").format.columnWidthPx = 620;

addSheet(workbook, "Surface Counts", asRows(
  ["Surface", "RouteCount", "DocsExactRouteCovered", "SkillsExactRouteCovered", "Notes"],
  surfaceRows));

addSheet(workbook, "API Inventory", asRows(
  ["Surface", "Method", "Route", "EndpointName", "SourceFile", "Line", "DocsCoveredByExactRoute", "SkillCoveredByExactRoute"],
  endpointRows));

addSheet(workbook, "Gap Map", asRows(
  ["Status", "Closure", "ID", "Area", "Priority", "Finding", "Evidence", "Repair", "Subbundle", "Validation"],
  gaps));

addSheet(workbook, "DTO Map", asRows(
  ["Surface", "DTO", "Fields", "Source", "Gap", "Subbundle"],
  buildDtoRows()));

addSheet(workbook, "Docs Skills", asRows(
  ["Artifact", "Type", "Status", "Gap", "Repair"],
  buildDocsSkillRows()));

addSheet(workbook, "Tool Parity", asRows(
  ["Surface", "HttpRoutes", "RuntimeTools", "Gap", "MissingAreas", "Source", "Subbundle"],
  buildToolParityRows()));

addSheet(workbook, "Gap Priority", asRows(
  ["Priority", "Count"],
  priorityRows));

addSheet(workbook, "Plan", asRows(
  ["Phase", "Name", "Classification", "Work", "Gate", "Validation"],
  buildPlanRows()));

addSheet(workbook, "Validation", asRows(
  ["Check", "Command", "Purpose"],
  buildValidationRows()));

await fs.mkdir(outputDir, { recursive: true });

const inspect = await workbook.inspect({
  kind: "sheet",
  sheetId: summarySheet.id,
  range: "A1:C10"
});
await fs.writeFile(inspectJson, JSON.stringify(inspect, null, 2));

const preview = await workbook.render({
  sheetName: "Summary",
  autoCrop: "all",
  scale: 1,
  format: "png"
});
await fs.writeFile(previewPng, new Uint8Array(await preview.arrayBuffer()));

const xlsx = await SpreadsheetFile.exportXlsx(workbook);
await xlsx.save(outputXlsx);

console.log(JSON.stringify({
  outputXlsx,
  previewPng,
  inspectJson,
  endpointCount: endpointRows.length,
  surfaces: surfaceRows
}, null, 2));
