import json
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path.cwd().resolve()
start = root / "src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj"
shell = root / "src/UI/CanDoItAll.Conversations.Shell/CanDoItAll.Conversations.Shell.csproj"
graph = {}
unresolved = []
def collect(path):
    path = path.resolve()
    if path in graph:
        return
    graph[path] = []
    for item in ET.parse(path).iter("ProjectReference"):
        include = item.get("Include")
        if not include:
            continue
        if "$(" in include or "*" in include:
            unresolved.append({"project": str(path), "include": include})
            continue
        target = (path.parent / include.replace("\\", "/")).resolve()
        if not target.exists():
            raise RuntimeError(f"Missing project reference: {target}")
        graph[path].append(target)
        collect(target)
collect(start)
visiting, done, cycles = [], set(), []
def visit(path):
    if path in visiting:
        cycles.append([str(p) for p in visiting[visiting.index(path):] + [path]])
        return
    if path in done:
        return
    visiting.append(path)
    for target in graph[path]:
        visit(target)
    visiting.pop()
    done.add(path)
visit(start)
result = {"root": str(start.relative_to(root)), "projects": len(graph), "edges": sum(map(len, graph.values())),
          "workbenchReferencesConversationShell": shell in graph[start],
          "shellReferencesWorkbench": start in graph.get(shell, []),
          "cycles": cycles, "unresolved": unresolved}
destination = root / ".artifacts/premerge-finishing-20260831/dependency-graph.json"
destination.write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result))
if cycles or unresolved or shell not in graph[start]:
    raise SystemExit(1)
