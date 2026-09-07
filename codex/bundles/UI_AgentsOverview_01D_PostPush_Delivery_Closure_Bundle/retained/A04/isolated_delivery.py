from pathlib import Path
import argparse
import datetime
import gzip
import hashlib
import json
import subprocess
import tarfile
import xml.etree.ElementTree as ET

root = next(p for p in Path(__file__).resolve().parents if (p / "CanDoItAll.slnx").exists())
parser = argparse.ArgumentParser()
parser.add_argument("--layout-root", type=Path, default=root / ".mcp-state" / "overview01d" / "isolated")
layout = parser.parse_args().layout_root
if layout.exists():
    raise SystemExit("A fresh isolated directory is required.")
layout.mkdir(parents=True)
retained = layout / "receipts"
retained.mkdir()
for boundary in ["Directory.Build.props", "Directory.Build.targets"]:
    (layout / boundary).write_text("<Project />\n", encoding="utf-8")
(layout / "global.json").write_bytes((root / "global.json").read_bytes())
allowed = {
    "CanDoItAll": {".gitignore", ".gitattributes", "tools/Validation/Portability/portability-risk-baseline.json", "src/Modules/CanDoItAll.Modules.AgentFramework/AgentFrameworkAgentsChatContextBuilder.cs", "src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentTeamDetailsDialog.razor", "tests/Unit/CanDoItAll.Tests.Unit/AgentFrameworkModuleChatContextBuilderTests.cs", "tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/AgentsOverviewSessionTests.cs", "tests/Components/CanDoItAll.Tests.Components/AgentsOverviewEffectLifecycleTests.cs"},
    "CanDoItAll.Components": {"src/CanDoItAll.Components.BaseLib/Components/Modals/DialogService.cs", "src/CanDoItAll.Components.BaseLib/README.md", "tests/CanDoItAll.Components.BaseLib.Tests/DialogNavigationOwnershipTests.cs", "tests/CanDoItAll.Components.BaseLib.Tests/fixtures/approvals/standard-public-api.metadata.approved.json", "tests/CanDoItAll.Components.BaseLib.Tests/fixtures/approvals/standard-source-package-inputs.approved.txt"},
    "CanDoItAll.FileTools": set()
}
prefixes = ("codex/bundles/UI_AgentsOverview_01D_PostPush_Delivery_Closure_Bundle/", "codex/bundles/UI_AgentsOverview_01_State_Read_Seams_Bundle/", "codex/bundles/UI_AgentGovernance_01_State_Read_Seams_Bundle/", "tools/Validation/bundles/")
def git(repo, *args):
    return subprocess.check_output(["git", "-C", str(repo), *args])

identities = []
for name in allowed:
    repo = root if name == "CanDoItAll" else root.parent / name
    if Path(git(repo, "rev-parse", "--show-toplevel").decode().strip()).resolve() != repo.resolve():
        raise SystemExit("An actual repository checkout is required: " + name)
    head = git(repo, "rev-parse", "HEAD").decode().strip()
    changed = {p.decode() for p in git(repo, "diff", "HEAD", "--name-only", "-z").split(b"\0") if p}
    changed.update(p.decode() for p in git(repo, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0") if p)
    unexpected = {p for p in changed if p not in allowed[name] and not (name == "CanDoItAll" and p.startswith(prefixes))}
    if unexpected:
        raise SystemExit("Unowned changes: " + str(unexpected))
    destination = layout / name
    destination.mkdir()
    archive = layout / (name + ".tar")
    with archive.open("wb") as output:
        subprocess.run(["git", "-C", str(repo), "archive", "--format=tar", head], stdout=output, check=True)
    with tarfile.open(archive) as source:
        for member in source.getmembers():
            target = (destination / member.name).resolve()
            if not target.is_relative_to(destination.resolve()) or member.issym() or member.islnk():
                raise SystemExit("Unsafe archive member: " + member.name)
        source.extractall(destination, filter="data")
    overlays = []
    for relative in sorted(changed):
        source = repo / relative
        if not source.is_file():
            raise SystemExit("Unexpected deletion: " + relative)
        data = source.read_bytes()
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        overlays.append({"path": relative, "sha256": hashlib.sha256(data).hexdigest()})
    rows = [{"path": p.relative_to(destination).as_posix(), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(destination.rglob("*")) if p.is_file()]
    (retained / (name + "-isolated-inventory.json.gz")).write_bytes(gzip.compress(json.dumps(rows, sort_keys=True).encode(), mtime=0))
    identities.append({"repository": name, "head": head, "tree": git(repo, "rev-parse", "HEAD^{tree}").decode().strip(), "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(), "files": len(rows), "overlays": overlays})
    print("Isolated", name, len(rows), "files and", len(overlays), "owned overlays", flush=True)
(retained / "source-layout.json").write_text(json.dumps(identities, indent=2) + "\n", encoding="utf-8")

commands = []
def run(name, command, cwd):
    start = datetime.datetime.now(datetime.timezone.utc).isoformat()
    process = subprocess.run(command, cwd=cwd, capture_output=True)
    output = process.stdout + process.stderr
    (retained / (name + ".txt.gz")).write_bytes(gzip.compress(output, mtime=0))
    commands.append({"name": name, "command": command, "cwd": str(cwd), "exit": process.returncode, "startedUtc": start, "finishedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat()})
    (retained / "commands.json").write_text(json.dumps(commands, indent=2) + "\n", encoding="utf-8")
    print(name, process.returncode, flush=True)
    if process.returncode:
        print(json.dumps(output.decode("utf-8", "replace")[-6000:]), flush=True)
        raise SystemExit(process.returncode)

primary = layout / "CanDoItAll"
components = layout / "CanDoItAll.Components"
filetools = layout / "CanDoItAll.FileTools"
roots = ["-p:UseLocalCanDoItAllLibraries=true", "-p:CanDoItAllComponentsRepositoryRoot=" + str(components), "-p:CanDoItAllFileToolsRepositoryRoot=" + str(filetools)]
for project in ["src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj", "src/App/CanDoItAll.Web/CanDoItAll.Web.csproj"]:
    run("isolated-" + Path(project).stem, ["dotnet", "build", project, "-c", "Release", "/m:1", "-p:UseSharedCompilation=false", "-v:q", *roots], primary)
version = "0.3.0-overview01d." + datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d%H%M%S")
feed = layout / "feed"
feed.mkdir()
for package in ["Common", "BaseLib"]:
    run("pack-" + package, ["dotnet", "pack", "src/CanDoItAll.Components." + package + "/CanDoItAll.Components." + package + ".csproj", "-c", "Release", "-o", str(feed), "-p:Version=" + version, "-p:PackageVersion=" + version, "/m:1", "-v:q"], components)
consumer = layout / "consumer"
consumer.mkdir()
(consumer / "Consumer.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><PackageReference Include="CanDoItAll.Components.BaseLib" Version="' + version + '"/></ItemGroup></Project>\n', encoding="utf-8")
(consumer / "Program.cs").write_text('''using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

using var dialogs = new DialogService(new Navigation());
using var lease = dialogs.PreserveDialogsOnSamePageNavigation();
using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
var result = dialogs.OpenAsync("Canceled", _ => builder => builder.AddContent(0, "Content"), cancellationToken: cancellation.Token);
if (!result.IsCanceled || dialogs.Dialogs.Count != 0) {
    throw new InvalidOperationException("Packed cancellation contract failed.");
}
Console.WriteLine("Packed external consumer passed.");

sealed class Navigation : NavigationManager {
    public Navigation() {
        Initialize("https://example.invalid/", "https://example.invalid/agents");
    }
    protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
}
''', encoding="utf-8")
config = ET.Element("configuration")
sources = ET.SubElement(config, "packageSources")
ET.SubElement(sources, "clear")
ET.SubElement(sources, "add", {"key": "delivery-local", "value": str(feed.resolve())})
ET.SubElement(sources, "add", {"key": "nuget.org", "value": "https://api.nuget.org/v3/index.json"})
ET.ElementTree(config).write(consumer / "NuGet.Config", encoding="utf-8", xml_declaration=True)
run("external-restore", ["dotnet", "restore", "Consumer.csproj", "--configfile", "NuGet.Config"], consumer)
assets = json.loads((consumer / "obj" / "project.assets.json").read_text(encoding="utf-8"))
project_references = [key for key, value in assets["libraries"].items() if value["type"] == "project"]
if project_references:
    raise SystemExit("External consumer contains unexpected project references.")
(retained / "external-package-graph.json").write_text(json.dumps({
    "packages": {key: value["type"] for key, value in assets["libraries"].items()},
    "projectReferences": project_references,
    "outerBuildImportsBlocked": True
}, indent=2) + "\n", encoding="utf-8")
run("external-build", ["dotnet", "build", "Consumer.csproj", "-c", "Release", "--no-restore", "/m:1", "-v:q"], consumer)
run("external-execute", ["dotnet", "run", "--project", "Consumer.csproj", "-c", "Release", "--no-build", "--no-restore"], consumer)
(retained / "package-receipt.json").write_text(json.dumps({"version": version, "published": False, "packages": [{"name": p.name, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(feed.glob("*.nupkg"))]}, indent=2) + "\n", encoding="utf-8")
