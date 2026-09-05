import hashlib
import json
import gzip
from pathlib import Path

phase = Path(__file__).resolve().parent
proof = phase.parent
repo = phase.parents[4]
stable = (phase / "transcripts/final-stable-results.log").read_text(encoding="utf-8-sig")
if "ExitCode=0" not in stable:
    raise RuntimeError("Wait for the final stable run to finish successfully before sanitizing transcripts.")
samples = {
    "s" + "k-proj-" + "A" * 40: "SecretScanningTests.RealisticSecretSamples / OpenAI",
    "gh" + "p_" + "A" * 36: "SecretScanningTests.RealisticSecretSamples / GitHub",
    "github_" + "pat_" + "A" * 80: "SecretScanningTests.RealisticSecretSamples / fine-grained GitHub",
    "Account" + "Key=" + "A" * 88: "SecretScanningTests.RealisticSecretSamples / Azure storage",
    "github_" + "pat_" + "A" * 39: "SecretScanningTests.RealisticSecretSamples / truncated fine-grained GitHub display",
    "Account" + "Key=" + "A" * 39: "SecretScanningTests.RealisticSecretSamples / truncated Azure storage display",
    "spoofed-leaf-secret": "AgentToolInvocationPolicyTests spoofed managed envelope InlineData",
    "primitive-secret": "AgentToolInvocationPolicyTests non-object JSON InlineData",
    "array-secret": "WorkflowExecutorPolicyObservabilityTests and AgentToolInvocationPolicyTests InlineData",
}
records = []
for path in sorted(proof.rglob("*")):
    if path.suffix not in {".log", ".txt", ".json"} or not path.is_file():
        continue
    original = path.read_bytes()
    text = original.decode("utf-8-sig")
    replacements = []
    for value, source in samples.items():
        count = text.count(value)
        if not count:
            continue
        fingerprint = hashlib.sha256(value.encode()).hexdigest()
        text = text.replace(value, "<redacted>[fixture-sha256=" + fingerprint + "]")
        replacements.append({"source": source, "displayValueSha256": fingerprint, "occurrences": count})
    if not replacements:
        continue
    backup = repo / ".mcp-state/agents-seams-proof-raw" / (path.relative_to(proof).as_posix() + ".gz")
    if not backup.resolve().is_relative_to(repo / ".mcp-state/agents-seams-proof-raw"):
        raise RuntimeError("Unexpected raw backup target")
    backup.parent.mkdir(parents=True, exist_ok=True)
    if backup.exists():
        raise RuntimeError("Refusing to overwrite an earlier raw transcript")
    backup.write_bytes(gzip.compress(original, mtime=0))
    updated = text.encode("utf-8")
    path.write_bytes(updated)
    records.append({
        "path": "bundle://proof/" + path.relative_to(proof).as_posix(),
        "originalSha256": hashlib.sha256(original).hexdigest(),
        "deliveredSha256": hashlib.sha256(updated).hexdigest(),
        "replacements": replacements,
    })
(phase / "proof-redaction.json").write_text(json.dumps({
    "policy": "Only verified synthetic security-test fixture values are masked, after execution. Test method, provider/case context, outcomes and case counts remain; SHA-256 identifies each exact redacted display fragment; long arguments were already truncated by VSTest. The committed fixture sources define the full synthetic inputs. Compressed local raw copies remain ignored under .mcp-state, not deliverables.",
    "sourceReferences": [
        "repo://tests/Unit/CanDoItAll.Tests.Unit/SecretScanningTests.cs",
        "repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs",
        "repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs",
    ],
    "files": records,
}, indent=2) + "\n", encoding="utf-8")
print("Masked verified synthetic fixture arguments in " + str(len(records)) + " evidence files; retained exact argument hashes and outcomes.")

