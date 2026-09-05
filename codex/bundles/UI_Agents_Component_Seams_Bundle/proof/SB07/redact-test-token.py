import gzip
import hashlib
import json
import re
from pathlib import Path

phase = Path(__file__).resolve().parent
repo = phase.parents[4]
path = phase / "transcripts/final-stable-results.log"
original = path.read_bytes()
text = original.decode("utf-8-sig")
pattern = re.compile(r"([?&]access_token=)([^\s]+)")
values = [match.group(2) for match in pattern.finditer(text)]
if len(values) != 2 or len(set(values)) != 1:
    raise RuntimeError("Unexpected query-token finding count")
token = values[0]
fingerprint = hashlib.sha256(token.encode()).hexdigest()
if fingerprint[:16] != "6799f6a5225a3273":
    raise RuntimeError("Query-token identity differs from the reviewed scanner finding")
replacement = "<redacted>[test-query-token-sha256=" + fingerprint + "]"
updated = text.replace(token, replacement).encode("utf-8")
path.write_bytes(updated)
path.with_suffix(".log.gz").write_bytes(gzip.compress(updated, mtime=0))
backup = repo / ".mcp-state/agents-seams-proof-raw/SB07/transcripts/final-stable-results.log.gz"
raw_backup = gzip.decompress(backup.read_bytes())
masked_backup = raw_backup.replace(token.encode(), replacement.encode())
backup.write_bytes(gzip.compress(masked_backup, mtime=0))
record = {
    "source": "repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiIntegrationTests.cs:303",
    "finding": "The auth test logs a freshly issued disposable-host token while proving query-string authentication is rejected. It is credential material and is removed, not classified as a synthetic constant.",
    "path": "bundle://proof/SB07/transcripts/final-stable-results.log.gz",
    "tokenSha256": fingerprint, "occurrences": 2,
    "beforeTranscriptSha256": hashlib.sha256(original).hexdigest(),
    "afterTranscriptSha256": hashlib.sha256(updated).hexdigest(),
    "backupPolicy": "The local compressed backup now also masks the token. It retains original synthetic fixture display values, but is no longer byte-identical to the original credential-bearing output. Original hashes remain evidence of the redaction chain.",
    "backupTokenOccurrencesRemoved": raw_backup.count(token.encode()),
}
(phase / "test-token-redaction.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
for candidate in [path.read_bytes(), gzip.decompress(path.with_suffix(".log.gz").read_bytes()), gzip.decompress(backup.read_bytes())]:
    if token.encode() in candidate:
        raise RuntimeError("Token remains in an owned transcript copy")
print("Removed both disposable integration-token occurrences from transcript, delivered gzip and local backup; outcomes unchanged.")

