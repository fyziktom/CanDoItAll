from __future__ import annotations

import argparse
from collections import Counter
import copy
import gzip
import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import unquote

PHASE = Path(__file__).resolve().parent
BUNDLE = PHASE.parents[1]
REPO = PHASE.parents[4]
BASELINE = "68db2ee0e63a2ce6baa681e9722acc0a67877b21"
MODULE = "src/Modules/CanDoItAll.Modules.AgentFramework/"
SIBLINGS = {
    "CanDoItAll.Components": "c3e6aa03a878994c0ba8aed6af017d0be75f3796",
    "CanDoItAll.FileTools": "7c7453c6583365ae5bd63f8fc6efc4a776e15818",
}
GENERATED = {
    "MANIFEST.sha256", "proof/SB07/changed-files.json",
    "proof/SB07/artifacts.json", "proof/SB07/transcripts/final-verifier.log",
}
FINAL_LOGS = [
    "proof/SB06/transcripts/component-browser-corrected-build.log",
    "proof/SB06/transcripts/component-browser-corrected-results.log",
    "proof/SB06/transcripts/unit-build.log",
    "proof/SB06/transcripts/unit-results.log",
    *["proof/SB07/transcripts/final-" + label + ".log" for label in
      ["solution-restore", "solution-build", "stable-restore", "stable-build", "stable-discovery"]],
    "proof/SB07/transcripts/final-stable-results.log.gz",
]
CURRENT = set(FINAL_LOGS + [
    "proof/SB06/transcripts/component-browser-corrected-discovery.log",
    "proof/SB06/transcripts/unit-discovery.log",
    "proof/SB07/transcripts/final-stable-expected-cases.txt",
    "proof/SB07/transcripts/portability-browser-corrected.log",
    "proof/SB07/portability-browser-corrected-scan.json.gz",
    "proof/SB07/browser-final-actions.json", "proof/SB07/browser-report.md",
    "proof/SB07/transcripts/browser-final-console.log",
])
NEGATIVE = [
    "proof/SB05/transcripts/lifetime-behavior-first-results.log",
    "proof/SB05/transcripts/target-echo-settled-results.log",
    "proof/SB05/transcripts/host-result-lifetime-results.log",
    "proof/SB06/transcripts/catalog-refresh-first.log",
    "proof/SB06/transcripts/page-save-echo-first.log",
]

def require(condition, message):
    if not condition:
        raise RuntimeError(message)

def git(*args, cwd=REPO):
    return subprocess.check_output(["git", *args], cwd=cwd)

def sha(data):
    return hashlib.sha256(data).hexdigest()

def read(path):
    if path.suffix == ".gz":
        with gzip.open(path, "rt", encoding="utf-8-sig") as stream:
            return stream.read()
    return path.read_text(encoding="utf-8-sig")

def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()

def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

def included():
    data = git("ls-files", "--cached", "--others", "--exclude-standard", "-z")
    return sorted(set(item.decode() for item in data.split(b"\0") if item))

def changed():
    data = git("diff", "--name-only", "-z", BASELINE)
    new = git("ls-files", "--others", "--exclude-standard", "-z")
    return sorted(set(item.decode() for item in (data + new).split(b"\0") if item))

def file_record(relative):
    path = REPO / relative
    result = subprocess.run(["git", "show", BASELINE + ":" + relative],
                            cwd=REPO, capture_output=True)
    return {
        "path": "repo://" + relative,
        "beforeSha256": sha(result.stdout) if result.returncode == 0 else None,
        "afterSha256": sha(path.read_bytes()) if path.exists() else None,
        "baselinePresence": "present" if result.returncode == 0 else "absent",
    }

def identities():
    require(git("rev-parse", "HEAD").decode().strip() == BASELINE, "Repository HEAD drift")
    values = {}
    for name, expected in SIBLINGS.items():
        sibling = REPO.parent / name
        actual = git("rev-parse", "HEAD", cwd=sibling).decode().strip()
        status = git("status", "--porcelain", cwd=sibling).decode().strip()
        require(actual == expected and not status, "Sibling drift: " + name)
        values[name] = {"commit": actual, "dirty": False, "mode": "live sibling source"}
    return values

def audit():
    identities()
    changed_source = [p for p in changed() if p.startswith(("src/", "tests/"))]
    require(changed_source, "No changed production/test files")
    for relative in changed_source:
        content = read(REPO / relative)
        require("GetUninitializedObject" not in content, "Uninitialized harness: " + relative)
        require("NotImplementedException" not in content, "Stub implementation: " + relative)
        if relative.startswith("src/"):
            require("IServiceProvider" not in content, "Service locator: " + relative)
    checks = {
        "Services/AgentEditorCommands.cs": ["workspace.SaveAgentAsync", "workspace.DeleteAgentAsync", "workspace.VerifyCapabilityAsync"],
        "Services/AgentEditorReads.cs": ["workspace.GetAgentEditorAsync", "workspace.ListCapabilitiesAsync"],
        "Services/AgentCatalogOperations.cs": ["workspace.ListAgentTeamsAsync", "workspace.UpdateAgentTeamMembersAsync"],
        "Services/AgentFrameworkUiServiceCollectionExtensions.cs": ["TryAddScoped<IAgentEditorCommands, AgentEditorCommands>", "TryAddScoped<IAgentEditorReads, AgentEditorReads>"],
        "Pages/Components/AgentCatalogHost.razor": ["IsCurrentEditorResult(presentationId, result)", "openedRequestedAgentId = result.AgentId.Value"],
        "Pages/Components/AgentDetailsDialog.razor": ["@key=", "EditForm"],
    }
    for relative, expected in checks.items():
        content = read(REPO / MODULE / relative)
        for marker in expected:
            require(marker in content, "Missing real producer/consumer: " + relative + ": " + marker)
        print("Production assertion PASS: repo://" + MODULE + relative)
    catalog = read(REPO / MODULE / "Pages/Components/AgentCatalogPanel.razor.cs")
    require(not re.search(r"\[Inject\]|IAgentWorkspace|DialogService|DbContext", catalog), "Controlled catalog retains effects")
    for parent in ["Pages/AgentsHomePage.razor.cs", "Pages/Components/AgentDetailsDialog.razor.cs"]:
        content = read(REPO / MODULE / parent)
        require(not re.search(r"IAgentWorkspaceService|AppDbContext|ISecretService", content), "Parent I/O leak: " + parent)
    for relative in changed():
        require(not relative.endswith((".csproj", ".slnx", ".props", ".targets")), "Unexpected build graph mutation")
    print("PASS INV-STATE INV-SESSION INV-WRITE INV-COMPOSITION: source anti-stub/ownership audit")
    print("Task.FromResult of supplied optional catalog/reference data is reviewed intentional input reuse, not an I/O stub.")
    print("This structural audit supplements actual adapter, adversarial, registered composition and browser proof.")

def documentation():
    metadata = json.loads(read(BUNDLE / "bundle.json"))
    require(metadata["executionAuthorizedByCurrentRequest"], "Missing owner authority")
    require(len(metadata["subbundles"]) == 7, "Missing work unit")
    for unit in metadata["subbundles"]:
        require((BUNDLE / "subbundles" / unit / "README.md").is_file(), "Missing phase: " + unit)
    required_ids = set(re.findall(r"R-\d{3}", read(BUNDLE / "requirements/00-normalized-requirements.md")))
    audited_ids = set(re.findall(r"R-\d{3}", read(PHASE / "execution-requirements.md")))
    require(required_ids == audited_ids, "Requirement audit coverage mismatch")
    count = 0
    for relative in included():
        path = REPO / relative
        if not path.is_relative_to(BUNDLE) or path.suffix != ".md" or "proof" in path.relative_to(BUNDLE).parts:
            continue
        for target in re.findall(r"(?<!!)\[[^\]]*\]\(([^)]+)\)", read(path)):
            target = unquote(target.strip().strip("<>").split("#")[0])
            if not target or re.match(r"\w+:", target) or "*" in target:
                continue
            require((path.parent / target).exists(), "Broken documentation link: " + relative + " -> " + target)
            count += 1
    for relative in included():
        path = REPO / relative
        if path.is_relative_to(BUNDLE) and path.suffix == ".json":
            json.loads(read(path))
    require(not git("diff", "--check").strip(), "Whitespace errors")
    print(f"PASS JSON, seven semantic work units, {count} local document links, git diff --check")

def gate_counts(discovery, results, expected, discovery_count=None, expansions=None):
    discovered = [line.strip() for line in read(BUNDLE / discovery).splitlines()
                  if line.startswith("    CanDoItAll.")]
    transcript = read(BUNDLE / results)
    require(len(discovered) == (discovery_count or expected), f"Discovery mismatch: {discovery}: {len(discovered)}")
    require(re.search(r"^ExitCode=0\s*$", transcript, re.M), "Gate not completed successfully: " + results)
    totals = [int(n.replace(",", "").replace(" ", "")) for n in
              re.findall(r"^\s*(?:Úspěšné|Passed):\s*([\d, ]+)\s*$", transcript, re.M)]
    require(sum(totals) == expected, f"Passed count mismatch: {results}: {totals} != {expected}")
    require(not re.search(r"^\s*(?:Neúspěšné|Failed|Přeskočené|Skipped):\s*[1-9]", transcript, re.M),
            "Failed or skipped tests: " + results)
    require(len(re.findall(r"^\s+(?:Úspěšné|Passed) CanDoItAll\.", transcript, re.M)) == expected,
            "Missing individual passing cases: " + results)
    expected_methods = Counter(case.split("(")[0] for case in discovered)
    for method, rows in (expansions or {}).items():
        require(expected_methods[method] == 1, "Unexpected dynamic theory discovery: " + method)
        expected_methods[method] = rows
    require(sum(expected_methods.values()) == expected, "Expanded expected count mismatch")
    actual_methods = Counter(case.split("(")[0].split(" [")[0] for case in re.findall(r"^\s+(?:Úspěšné|Passed) (CanDoItAll\..+)$", transcript, re.M))
    require(actual_methods == expected_methods, "Discovered/executed method and theory count mismatch: " + results)
    return {"expected": expected, "passed": sum(totals), "failed": 0, "skipped": 0,
            "discovery": "bundle://" + discovery, "results": "bundle://" + results}

def gates():
    theory_data = json.loads(read(PHASE / "stable-theory-expansions.json"))
    expansions = {item["method"]: item["executedRows"] for item in theory_data["expansions"]}
    for item in theory_data["expansions"]:
        require(git("show", BASELINE + ":" + item["source"][7:]) == (REPO / item["source"][7:]).read_bytes().replace(b"\r\n", b"\n"), "Dynamic theory source changed: " + item["method"])
    outcomes = [
        gate_counts("proof/SB06/transcripts/component-browser-corrected-discovery.log", FINAL_LOGS[1], 130),
        gate_counts("proof/SB06/transcripts/unit-discovery.log", FINAL_LOGS[3], 28),
        gate_counts("proof/SB07/transcripts/final-stable-discovery.log", FINAL_LOGS[-1], 9597, 9542, expansions),
    ]
    for relative in FINAL_LOGS:
        require(re.search(r"^ExitCode=0\s*$", read(BUNDLE / relative), re.M), "Failed/incomplete gate: " + relative)
    portability = read(PHASE / "transcripts/portability-browser-corrected.log")
    require("RESULT: PASS (14251" in portability and "ExitCode=0" in portability, "Portability enforcement missing")
    with gzip.open(PHASE / "portability-browser-corrected-scan.json.gz", "rt", encoding="utf-8-sig") as stream:
        json.load(stream)
    secret_scan = json.loads(read(PHASE / "artifact-secret-scan-final.json"))
    require(not secret_scan["findings"], "Unresolved artifact secret finding")
    provider_patterns = [r"(?<![A-Za-z0-9_-])s" + r"k-[A-Za-z0-9_-]{20,}", r"gh[pousr]_[A-Za-z0-9_]{30,}", r"github_" + r"pat_[A-Za-z0-9_]{20,}", r"Account" + r"Key=[A-Za-z0-9+/]{60,}={0,2}"]
    for relative in included():
        path = REPO / relative
        if path.is_relative_to(BUNDLE / "proof") and path.suffix in {".md", ".json", ".log", ".txt", ".ps1", ".py"}:
            require(not any(re.search(pattern, read(path)) for pattern in provider_patterns), "Provider-shaped value in delivered proof: " + relative)
    console = read(PHASE / "transcripts/browser-final-console.log")
    require("Errors: 0, Warnings: 0" in console, "Browser console failure")
    browser = read(PHASE / "browser-final-actions.json")
    require('catalogCards\\":29' in browser and 'proofAgents\\":0' in browser and 'proofTeams\\":0' in browser,
            "Missing final browser cleanup evidence")
    for relative in NEGATIVE:
        require(re.search(r"\[FAIL\]|Neúspěšné|Failed", read(BUNDLE / relative)), "Missing adversarial failure: " + relative)
    return outcomes

def source_association():
    started = re.search(r"^Started=(.+)$", read(PHASE / "transcripts/final-solution-build.log"), re.M).group(1).strip()
    cutoff = datetime.fromisoformat(started).timestamp()
    files = []
    for relative in changed():
        if not relative.startswith(("src/", "tests/")):
            continue
        path = REPO / relative
        require(path.stat().st_mtime <= cutoff, "Source changed after final build began: " + relative)
        files.append({"path": "repo://" + relative, "sha256": sha(path.read_bytes()),
                      "modifiedUtc": datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat()})
    binary_paths = [
        MODULE + "bin/Release/net10.0/CanDoItAll.Modules.AgentFramework.dll",
        "src/App/CanDoItAll.Web/bin/Release/net10.0/CanDoItAll.Modules.AgentFramework.dll",
        "tests/Components/CanDoItAll.Tests.Components/bin/Release/net10.0/CanDoItAll.Modules.AgentFramework.dll",
        "tests/Unit/CanDoItAll.Tests.Unit/bin/Release/net10.0/CanDoItAll.Modules.AgentFramework.dll",
    ]
    binaries = [{"path": "repo://" + relative, "sha256": sha((REPO / relative).read_bytes())}
                for relative in binary_paths]
    require(len({item["sha256"] for item in binaries}) == 1, "Browser/test module binary mismatch")
    write_json(PHASE / "source-build-association.json", {
        "baseline": BASELINE, "buildStarted": started, "files": files, "binaries": binaries,
        "meaning": "All changed source/test bytes predate the final solution build. Module, browser host and both focused test output copies contain the identical module assembly. Final verifier rejects later source drift; binaries are local build outputs, not delivered artifacts.",
    })
    print(f"PASS source/build association: {len(files)} source/test files; four identical module DLL copies.")

def freeze():
    outcomes = gates()
    state = identities()
    prefix = BUNDLE.relative_to(REPO).as_posix() + "/"
    records = []
    for relative in changed():
        if relative.startswith(prefix):
            inside = relative[len(prefix):]
            if inside in GENERATED or inside.startswith("proof/SB"):
                continue
        records.append(file_record(relative))
    production = [record for record in records if record["path"].startswith(("repo://src/", "repo://tests/", "repo://tools/"))]
    source_digest = sha(canonical(production))
    write_json(PHASE / "changed-files.json", {
        "baseline": BASELINE, "branch": git("branch", "--show-current").decode().strip(),
        "sourcePatchSha256": source_digest, "siblings": state, "files": records,
        "hashSemantics": "Exact baseline git blob and working-tree bytes, including new files absent at baseline. Source patch identity hashes the canonical source/test/portability-baseline records.",
        "proofAndClosureFiles": "All included proof artifacts, including scripts/reports, are enumerated in artifacts.json. They are new at baseline (beforeSha256=null). Integrity metadata and verifier log are excluded from their own input hash graph and authenticated by root MANIFEST.sha256.",
    })
    artifacts = []
    for relative in included():
        if not relative.startswith(prefix + "proof/"):
            continue
        inside = relative[len(prefix):]
        if inside in GENERATED - {"proof/SB07/changed-files.json"}:
            continue
        path = REPO / relative
        status = "current-source" if inside in CURRENT or "/final-" in inside else "owning-phase-or-historical"
        artifacts.append({
            "path": "bundle://" + inside, "sha256": sha(path.read_bytes()), "bytes": path.stat().st_size,
            "beforeSha256": file_record(relative)["beforeSha256"], "association": status,
            "sourcePatchSha256": source_digest if status == "current-source" else None,
            "purpose": "Current acceptance evidence" if status == "current-source" else "Owning-phase report, reviewed contract, or labeled historical attempt; consult its manifest/run label",
        })
    write_json(PHASE / "artifacts.json", {
        "createdUtc": datetime.now(timezone.utc).isoformat(), "baseline": BASELINE,
        "sourcePatchSha256": source_digest, "gates": outcomes, "artifacts": artifacts,
        "exclusions": sorted(GENERATED - {"proof/SB07/changed-files.json"}),
        "integrityOrder": "changed-files -> artifacts -> verifier log -> root MANIFEST; final read-only verify also checks root MANIFEST",
    })
    print(f"Frozen {len(records)} changed authored files and {len(artifacts)} proof artifacts; source patch {source_digest}")

def validate_artifacts(manifest):
    require(manifest["baseline"] == BASELINE, "Wrong artifact baseline")
    for item in manifest["artifacts"]:
        require(item["path"].startswith("bundle://"), "Nonportable artifact")
        path = BUNDLE / item["path"][9:]
        require(path.resolve().is_relative_to(BUNDLE), "Artifact escapes bundle")
        require(path.is_file(), "Missing artifact: " + item["path"])
        require(sha(path.read_bytes()) == item["sha256"], "Stale/tampered artifact: " + item["path"])

def verify(root=False):
    identities()
    manifest = json.loads(read(PHASE / "artifacts.json"))
    validate_artifacts(manifest)
    source = json.loads(read(PHASE / "changed-files.json"))
    association = json.loads(read(PHASE / "source-build-association.json"))
    for item in association["files"]:
        require(sha((REPO / item["path"][7:]).read_bytes()) == item["sha256"], "Post-build source drift: " + item["path"])
    for record in source["files"]:
        require(file_record(record["path"][7:]) == record, "Changed file drift: " + record["path"])
    require(gates() == manifest["gates"], "Gate results drift")
    for label, mutate in [
        ("wrong artifact hash", lambda data: data["artifacts"][0].update(sha256="0" * 64)),
        ("missing artifact", lambda data: data["artifacts"][0].update(path="bundle://proof/SB07/absent-proof.log")),
    ]:
        corrupted = copy.deepcopy(manifest)
        mutate(corrupted)
        try:
            validate_artifacts(corrupted)
        except RuntimeError:
            print("Verifier adversarial self-check rejected " + label)
        else:
            raise RuntimeError("Verifier accepted " + label)
    if root:
        for line in read(BUNDLE / "MANIFEST.sha256").splitlines():
            expected, relative = line.split("  ", 1)
            require(sha((BUNDLE / relative).read_bytes()) == expected, "Root manifest drift: " + relative)
    print(f"PASS final verifier: {len(source['files'])} changed files, {len(manifest['artifacts'])} artifacts, 130 + 28 focused and 9597 stable cases; no missing/stale proof.")
    print("INV-STATE INV-SESSION INV-WRITE INV-COMPOSITION INV-HANDOFF INV-PORTABILITY")
    print("Source patch SHA-256: " + manifest["sourcePatchSha256"])

def root_manifest():
    prefix = BUNDLE.relative_to(REPO).as_posix() + "/"
    lines = []
    for relative in included():
        if relative.startswith(prefix) and relative != prefix + "MANIFEST.sha256":
            lines.append(sha((REPO / relative).read_bytes()) + "  " + relative[len(prefix):])
    (BUNDLE / "MANIFEST.sha256").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"Root document/proof manifest: {len(lines)} files")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["audit", "docs", "source-association", "freeze", "verify", "root-manifest"])
    parser.add_argument("--root", action="store_true")
    options = parser.parse_args()
    if options.action == "verify":
        verify(options.root)
    else:
        {"audit": audit, "docs": documentation, "source-association": source_association, "freeze": freeze, "root-manifest": root_manifest}[options.action]()

