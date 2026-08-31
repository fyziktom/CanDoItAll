from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET

sb = Path(__file__).resolve().parent
repo = sb.parents[4]
print("Command: python codex/bundles/agent-startup-performance/proof/SB02/Audit-Sb02RetainedProof.py")
print("Working directory: repo://.")
print("Run label: offline-retained-sb02-anti-stub-audit")
print("StartedUtc: " + datetime.now(timezone.utc).isoformat())
print("This command reads frozen source and retained results only; no tests/builds/provider calls run.")
identities = json.loads((sb / "source-binary-hashes.json").read_text(encoding="utf-8-sig"))
for source in identities["sources"]:
    path = repo / source["path"]
    assert hashlib.sha256(path.read_bytes()).hexdigest() == source["sha256"].lower(), source["path"]
print("Source hashes verified: " + str(len(identities["sources"])))
loader = (repo / identities["sources"][0]["path"]).read_text(encoding="utf-8-sig")
materializer = (repo / identities["sources"][1]["path"]).read_text(encoding="utf-8-sig")
assert "sharedProviderMaterializer.Validate(provider, import, source).Shape is null" in loader
assert "HasSingleImport = imports.Count() == 1" in loader
assert "MapPersonal(provider).ConfigurationRevision" in loader
assert "SharedProviderPublicationSnapshotReader.TryRead(import" in materializer
assert "HasValidProfileCache(profile, source, publication, baseUri)" in materializer
for source in identities["sources"]:
    if source["role"] == "owned" and source["path"].startswith("src/"):
        assert "NotImplementedException" not in (repo / source["path"]).read_text(encoding="utf-8-sig")
print("No placeholder production implementation found; canonical validation/cardinality/local-map branches retained.")
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
phase_cases = {}
for phase, expected, failures in [("unit-candidate", 76, 0), ("integration-candidate", 35, 0), ("integration-characterized", 34, 0), ("integration-query-red", 1, 1)]:
    tree = ET.parse(sb / "transcripts" / (phase + ".trx"))
    cases = tree.findall(".//t:UnitTestResult", ns)
    assert len(cases) == expected
    assert sum(case.attrib["outcome"] == "Failed" for case in cases) == failures
    assert all(case.attrib["outcome"] in ("Passed", "Failed") for case in cases)
    phase_cases[phase] = cases
    print(phase + ": cases=" + str(expected) + ", failures=" + str(failures))
red_message = "\n".join(node.text or "" for node in phase_cases["integration-query-red"][0].findall("./t:Output/t:ErrorInfo/t:Message", ns))
assert re.search(r"Expected:\s*2", red_message)
assert re.search(r"Actual:\s*3", red_message)
requirements = {
    "SB02-I01": "Warm_single_and_set_lookups_reject_corruption_without_token_changes",
    "SB02-I02": "Validate_retains_canonical_shape_for_operationally_disabled_graphs",
    "SB02-I03": "Revision_probes_preserve_local_mapping_failure_without_token_changes",
    "SB02-I04": "Duplicate_imports_are_rejected_before_invalid_source_materialization",
    "SB02-I05": "Revision_set_preserves_invalid_unrelated_source_value_conversion_failure",
    "SB02-I06": "Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries",
    "SB02-I07": "AgentProviderCredentialDispatchScopeTests",
    "SB02-I08": "Projection_preserves_shared_origin_typed_credential_network_and_remote_capability_constraints",
    "SB02-I09": "Validate_avoids_effective_model_copy_allocations",
}
passing = phase_cases["unit-candidate"] + phase_cases["integration-candidate"]
for invariant, fragment in requirements.items():
    matches = [case.attrib["testName"] for case in passing if fragment in case.attrib["testName"] and case.attrib["outcome"] == "Passed"]
    assert matches, invariant
    print("INVARIANT-ID: " + invariant + "; retained passing cases=" + str(len(matches)) + "; source/test boundary=" + fragment)
print("No new secret cache, fallback or omitted canonical validator is claimed by this proof; manual source/architecture review remains separately retained.")
print("CompletedUtc: " + datetime.now(timezone.utc).isoformat())
print("ExitCode: 0")
