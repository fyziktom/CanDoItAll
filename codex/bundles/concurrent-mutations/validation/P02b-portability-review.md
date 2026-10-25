# P02b portability review

The provisional complete scan (including untracked proposed source) covered 8,300 files
and 36,195 findings without truncation. Enforcement against the committed baseline
reported 14 added executable-source fingerprints and no stale findings. This is not
final enforcement: source changed after that scan and must be regenerated before refresh.

All 14 additions were inspected in their actual source context:

- HTTP JSON property names use ordinal case-insensitive duplicate detection, matching
  the configured Web binder. This is protocol policy, not filesystem case equivalence.
- The two offline-command ContentRootPath references pass the framework-selected root
  to existing password-file configuration and module registration. No machine path is
  embedded and the command does not start an ordinary host.
- The activation coordination path combines the existing control-plane root with a
  fixed lock-file segment. Activation files use that same resolved private root,
  a fixed directory and a GUID-derived filename through the existing path policy.
  These produce six path/root findings across coordination and activation services.
- The activation FileStream finding is a bounded read opened with FileAccess.Read;
  writes use the existing durable atomic-file owner and the explicit activation lease.
- Four generated migration-designer findings copy existing RootPathState,
  RootPathSyntax, RaisesEscalation and EscalationCount model properties into the new
  historical model. They introduce no path interpretation or process execution.
- The governed binding hashes the explicit admitted McpServerKey as part of the effect
  witness. It does not discover, launch or configure an MCP process.

These are intentional portable changes. No scanner rule was weakened and the baseline
has not yet been refreshed. Regenerate the full scan, inspect any additional delta,
refresh only reviewed findings, inspect that diff, then enforce without --write-baseline.

The next complete frozen-candidate scan covered 8,314 files and 36,228 findings without
truncation. Its executable-source delta was exactly the same reviewed 14 additions and
no stale allowances. The baseline was refreshed to 15,437 allowances; its inspected diff
contains only those 14 records plus timestamp/count metadata. Enforcement without
--write-baseline passed. Rendering review also passed for 1,193 current inputs after
registering the Web host/store ownership, and documentation validation passed for 424
maintained Markdown files. Subsequent governed production repairs require regeneration
and enforcement again before the implementation checkpoint closes.

The post-transfer-repair scan (`p02bc-frozen-portability`) covered 8,332 files and
36,236 findings, including untracked files. It found no new executable-source findings
and one stale offline-command ContentRootPath allowance: the broad runtime-module
registration was removed, while the password-file root remains. Removing that stale
allowance produces 15,436 reviewed allowances. The complete diff retains the other 13
previously reviewed additions and changes only timestamp/count metadata. Final enforcement
without `--write-baseline` passed for that frozen source. Scanner support tests passed
6+4+7 cases; rendering review passed 1,195 inputs after the shared scoped browser-store
composition review. Newly identified package-import commit/compensation repairs will
require another complete scan and no-write enforcement before their checkpoint closes.

The next complete scan (`p02bc-final-portability`) covered 8,342 files and 36,256
findings. Its three added executable fingerprints are the OpenAPI UUID, caller
binding and epoch regular expressions. Each uses `(?![\\s\\S])` as an absolute
end-of-input assertion; the scanner sees the escaped backslashes as a possible
Windows path. These are transport-validation expressions, contain no filesystem
path, and intentionally reject trailing newlines across regex consumers. There
are no stale findings and no scanner-rule changes. A later test namespace-import
repair requires another complete scan before final enforcement.

Source secret review scanned a 6,707,962-byte text snapshot of 159 changed or new
source/test/tool files, with an explicit 10,000,000-byte scanner bound and no
skipped input. The default 5-MiB attempt scanned zero files and is not passing
coverage. The complete scan reported three assignment-pattern matches, all in
unchanged portions of WorkspaceDataSourcesOwner: lines 37/89 assign
`string.Empty`, and line 76 calls `password.Take()`. These are expressions, not
embedded credentials. No fixture-password sentinel was found. The raw scanner
status remains FAIL with those three reviewed false positives; it is not reported
as an unqualified scanner PASS. A source hash manifest and sanitized finding
report preserve coverage after disposal of the temporary source snapshot.

The post-wrapper-repair source snapshot covers the same 159 files at 6,737,643
bytes (`p02bc-checkpoint-source-manifest`). Its complete scan has the same three
reviewed rule/fingerprint matches and zero private-fixture sentinel matches.
The snapshot file and empty owned directory were removed and absence verified.
Final complete portability enforcement for that source passed without a write
(`p02bc-list-repair-portability`, 36,266 findings and unchanged 15,439 allowances).
Rendering review passed 1,195 inputs and maintained documentation passed 424 files.

The final browser-qualified source scan (`p02bc-browser-final-portability`) covered
8,350 files and 36,268 findings, with no truncation. No-write enforcement passed
against the same 15,439 reviewed allowances. The final heading's rendering hash
passed the 1,195-input register. Support-scanner tests remain the unchanged
6+4+7 passing cases; no scanner rule or reviewed allowance changed in these repairs.

`p02bc-qualified-source-manifest.json` covers 159 changed/new source, test and tool
files in a 6,745,763-byte snapshot. Its complete 10,000,000-byte-bound scan had
the same three WorkspaceDataSourcesOwner source-expression matches and zero
private-fixture password matches. The raw FAIL status is retained; these are the
previously reviewed `string.Empty` / `password.Take()` expressions, not values.
The snapshot file and empty owned directory were removed and absence verified.

`p02bc-qualified-proof-secret-scan.json` scanned 318 proof text files, including
all previously oversized inventories, with a 30,000,000-byte bound and no
oversized/unreadable/nontext exclusions. Orchestration extended the loaded
scanner's text suffix set with `.sql` and `.py`; the scanner source was unchanged.
Four findings in two Python probes are two rules matching each of two password
arguments. AST inspection confirms one reads the owned fixture password file at
runtime and the other refers to the `PASSWORD` variable. No literal password is
embedded and no fixture-password sentinel was found. These reviewed expression
false positives retain the scanner's raw FAIL status.

Six exact browser evidence roots were scanned separately (five native-host
directories recorded in resources plus the standalone reference sandbox).
Their seven text files passed with zero findings, no skipped oversized/unreadable
text and zero sentinel matches. Nine PNGs were excluded from text scanning and
visually reviewed. Reports are `p02bc-browser-proof-1` through `-6-secret-scan`.
These scopes cover this checkpoint's retained evidence, not future Broad Stable
or later implementation artifacts.
