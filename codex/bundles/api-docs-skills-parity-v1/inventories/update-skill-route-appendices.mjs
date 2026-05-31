import fs from "node:fs/promises";
import path from "node:path";

const repoRoot = path.resolve(process.cwd(), "../../..");
const apiDir = path.join(repoRoot, "src", "CanDoItAll.Web", "Api");
const projectStructureApi = path.join(repoRoot, "src", "CanDoItAll.Web", "ProjectStructureAgentApi.cs");

const mapMethodPattern = /(\w+)\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]*)"/;
const mapGroupPattern = /var\s+(\w+)\s*=\s*(\w+|endpoints)\.MapGroup\(\s*"([^"]*)"\s*\)/;

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

function inferSurface(route) {
  const normalized = normalizeRoute(route);
  if (normalized.startsWith("/api/cognitive-memory/v1")) {
    return "cognitive-memory-v1";
  }

  const match = normalized.match(/^\/api\/([^/]+)/);
  return match ? match[1] : "other";
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
            method: methodSuffix.toUpperCase(),
            route,
            surface: inferSurface(route),
            source: relativeFile
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
        method: methodSuffix.toUpperCase(),
        route,
        surface: inferSurface(route),
        source: relativeFile
      });
    }
  }

  return endpoints
    .filter(endpoint => endpoint.route.startsWith("/api/"))
    .sort((left, right) =>
      left.surface.localeCompare(right.surface)
      || left.route.localeCompare(right.route)
      || left.method.localeCompare(right.method));
}

function buildAppendix(title, endpoints) {
  const rows = endpoints.map(endpoint => `| \`${endpoint.method}\` | \`${endpoint.route}\` |`);
  return [
    "## Source Route Appendix",
    "",
    "<!-- api-docs-skills-parity:routes:start -->",
    "",
    `${title}. Generated from Minimal API registrations; rerun \`.codex/tmp/api-docs-skills-gap-map/update-skill-route-appendices.mjs\` when routes change.`,
    "",
    "| Method | Route |",
    "| --- | --- |",
    ...rows,
    "",
    "<!-- api-docs-skills-parity:routes:end -->",
    ""
  ].join("\n");
}

async function updateSkill(skillPath, title, endpoints) {
  const fullPath = path.join(repoRoot, skillPath);
  const original = await fs.readFile(fullPath, "utf8");
  const appendix = buildAppendix(title, endpoints);
  const startMarker = "<!-- api-docs-skills-parity:routes:start -->";
  const endMarker = "<!-- api-docs-skills-parity:routes:end -->";
  const existingStart = original.indexOf("## Source Route Appendix");
  const markerStart = original.indexOf(startMarker);
  const markerEnd = original.indexOf(endMarker);

  let updated;
  if (existingStart >= 0 && markerStart >= existingStart && markerEnd > markerStart) {
    updated = `${original.slice(0, existingStart).trimEnd()}\n\n${appendix}${original.slice(markerEnd + endMarker.length).trimStart()}`;
  } else {
    updated = `${original.trimEnd()}\n\n${appendix}`;
  }

  await fs.writeFile(fullPath, updated, "utf8");
}

const endpoints = await parseApiEndpoints();
const bySurface = new Map();
for (const endpoint of endpoints) {
  if (!bySurface.has(endpoint.surface)) {
    bySurface.set(endpoint.surface, []);
  }

  bySurface.get(endpoint.surface).push(endpoint);
}

await updateSkill(
  "codex/skills/candoitall-api-agents/SKILL.md",
  "Agents API route appendix",
  bySurface.get("agents") ?? []);
await updateSkill(
  "codex/skills/candoitall-api-workflows/SKILL.md",
  "Workflows API route appendix",
  bySurface.get("workflows") ?? []);
await updateSkill(
  "codex/skills/candoitall-api-processes/SKILL.md",
  "Processes API route appendix",
  bySurface.get("processes") ?? []);
await updateSkill(
  "codex/skills/candoitall-api-project-structure/SKILL.md",
  "Project Structure API route appendix",
  bySurface.get("project-structure") ?? []);
await updateSkill(
  "codex/skills/candoitall-api-cognitive-memory/SKILL.md",
  "Cognitive Memory API route appendix",
  [
    ...bySurface.get("cognitive-memory") ?? [],
    ...bySurface.get("cognitive-memory-v1") ?? []
  ]);

console.log(JSON.stringify({
  agents: bySurface.get("agents")?.length ?? 0,
  workflows: bySurface.get("workflows")?.length ?? 0,
  processes: bySurface.get("processes")?.length ?? 0,
  projectStructure: bySurface.get("project-structure")?.length ?? 0,
  cognitiveMemory: (bySurface.get("cognitive-memory")?.length ?? 0) + (bySurface.get("cognitive-memory-v1")?.length ?? 0)
}, null, 2));
