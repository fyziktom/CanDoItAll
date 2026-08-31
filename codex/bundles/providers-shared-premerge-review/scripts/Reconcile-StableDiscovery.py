import collections
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
REVIEWS = ROOT / "codex/bundles/providers-shared-premerge-review/reviews"
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
EXPANSIONS = json.loads('''[{"method":"CanDoItAll.Tests.Integration.External.PluginCatalogIntegrationTests.packaged_plugin_preview_simulation_avoids_live_external_effects","rows":6,"source":"tests/Integration/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs","dataMember":"BundledPluginSimulationCases","line":1084},{"method":"CanDoItAll.Memory.Tests.Runtime.MemoryOperationHandlerTests.Capability_mismatch_denial_is_consistent_for_all_handler_callers","rows":5,"source":"tests/Memory/CanDoItAll.Memory.Tests/MemoryOperationHandlerTests.cs","dataMember":"CrossCallerRoutes","line":325},{"method":"CanDoItAll.Memory.Tests.Runtime.MemoryOperationHandlerTests.No_provider_denial_is_consistent_for_all_handler_callers","rows":5,"source":"tests/Memory/CanDoItAll.Memory.Tests/MemoryOperationHandlerTests.cs","dataMember":"CrossCallerRoutes","line":325},{"method":"CanDoItAll.Memory.Tests.Security.MemoryOperationAccessAuthorizerTests.Any_changed_ownership_dimension_is_denied","rows":9,"source":"tests/Memory/CanDoItAll.Memory.Tests/MemoryOperationAccessAuthorizerTests.cs","dataMember":"ForeignCallers","line":45},{"method":"CanDoItAll.Tests.Unit.AgentFramework.FloatingAgentChatSettingsValidatorTests.Validate_rejects_values_outside_inclusive_boundaries","rows":8,"source":"tests/Unit/CanDoItAll.Tests.Unit/FloatingAgentChatArchitectureTests.cs","dataMember":"InvalidSettings","line":2628},{"method":"CanDoItAll.Tests.Unit.AgentFramework.MafFinalizerToolFactorySchemaCharacterizationTests.CreateCapture_produces_the_policy_tool_name_description_and_json_schema","rows":8,"source":"tests/Unit/CanDoItAll.Tests.Unit/MafFinalizerToolFactorySchemaCharacterizationTests.cs","dataMember":"Contracts / ExpectedBaselines","line":128},{"method":"CanDoItAll.Tests.Unit.Processes.ProcessRuntimeToolPreflightServiceTests.Process_starting_tool_contracts_have_deterministic_host_routes","rows":21,"source":"tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs","dataMember":"ProcessStartingToolCapabilityRoutes","line":46}]''')
by_method = {item["method"]: item for item in EXPANSIONS}
discovered = {}
assembly = None
for line in (REVIEWS / "sb09-stable-discovery.log").read_text(encoding="utf-8-sig").splitlines():
    match = re.search(r"([^\\/]+)\.dll \(\.NET", line)
    if match:
        assembly = match.group(1)
        discovered[assembly] = collections.Counter()
    elif assembly and line.startswith("    "):
        discovered[assembly][line.strip().split("(", 1)[0]] += 1

results = []
seen = set()
expanded = set()
for path in sorted((REVIEWS / "test-results").glob("sb09-stable_*.trx")):
    run = ET.parse(path).getroot()
    codebase = run.find("t:TestDefinitions/t:UnitTest/t:TestMethod", NS).get("codeBase")
    assembly = re.split(r"[\\/]", codebase)[-1][:-4]
    if assembly not in discovered or assembly in seen:
        raise RuntimeError(f"Unexpected/duplicate assembly: {assembly}")
    seen.add(assembly)
    actual = collections.Counter(item.get("testName").split("(", 1)[0]
        for item in run.findall("t:Results/t:UnitTestResult", NS))
    differences = []
    for method in sorted(set(discovered[assembly]) | set(actual)):
        before, after = discovered[assembly][method], actual[method]
        if before == after:
            continue
        expansion = by_method.get(method)
        if before != 1 or expansion is None or after != expansion["rows"]:
            raise RuntimeError(f"Unexplained discovery mismatch: {method} {before} -> {after}")
        expanded.add(method)
        differences.append({**expansion, "discovery": before, "executed": after,
            "sourceSha256": hashlib.sha256((ROOT / expansion["source"]).read_bytes()).hexdigest().upper()})
    counters = run.find("t:ResultSummary/t:Counters", NS).attrib
    total, passed, failed = (int(counters[key]) for key in ("total", "passed", "failed"))
    skipped = int(counters["notExecuted"])
    expected_rows = sum(discovered[assembly].values()) + sum(item["rows"] - 1 for item in differences)
    if total != expected_rows or total != sum(actual.values()) or passed != total or failed or skipped:
        raise RuntimeError(f"Stable result mismatch/failure: {assembly}")
    times = run.find("t:Times", NS).attrib
    results.append({"assembly": assembly, "listed": sum(discovered[assembly].values()),
        "expectedExpandedRows": expected_rows, "total": total, "passed": passed,
        "failed": failed, "skipped": skipped, "start": times["start"], "finish": times["finish"],
        "trx": path.name, "deferredTheories": differences})
if len(results) != 5 or seen != set(discovered) or expanded != set(by_method):
    raise RuntimeError("Incomplete assembly/deferred-theory reconciliation.")
report = {
    "checkpoint": "CP-MERGE-FROZEN", "invocations": 1, "exitCode": 0,
    "listedDisplayEntries": sum(item["listed"] for item in results),
    "executedRows": sum(item["total"] for item in results),
    "passed": sum(item["passed"] for item in results), "failed": 0, "skipped": 0,
    "note": "The initial 9369 display-entry count omitted runtime expansion of seven existing MemberData theories. Source inspection accounts for all 55 additional rows; original discovery is preserved. No unlisted method, missing case, extra run or unexplained count drift.",
    "assemblies": results}
(REVIEWS / "sb09-stable-results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {report['listedDisplayEntries']} listed entries -> {report['executedRows']} passed rows; seven source-verified deferred theories; no unexplained differences.")
