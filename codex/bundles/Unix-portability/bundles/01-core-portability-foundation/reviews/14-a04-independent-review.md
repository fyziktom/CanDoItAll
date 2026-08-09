# A04 independent Security Gate C2 review

## Decision

**NO-GO — Security Gate C2 is blocked.**

A05 must remain blocked. The supplied Windows, Linux, headless, build, architecture,
and focused test evidence is green for its named scopes, but the bundle's explicit
actual-macOS condition is unmet and two additional security/recovery findings prevent
closure of SEC-008, SEC-011, and SEC-013.

## Findings

| Severity | Requirement | Finding and evidence | Required action |
|---|---|---|---|
| Blocking / Critical | SEC-002, A04-T11 | Actual macOS Keychain evidence is absent. SEC-002 requires create/read/update/delete/restart/concurrency tests to run on macOS and prove access-control behavior (`requirements/requirements.json:281-283`). A04-T11 explicitly requires actual Windows/Linux/macOS/headless restart and migration evidence before platform composition continues (`tasks.md:70-72`), and validation requires a macOS result when platform behavior changes (`validation.md:20`). The evidence report confirms genuine macOS execution is unavailable (`reviews/13-a04-evidence-report.md:6,81`). The injected-native contract test and a test method that returns without exercising Keychain on non-macOS hosts cannot replace Darwin/Security.framework execution; Docker is Linux, not macOS. | Run the existing actual Keychain integration on a genuine macOS user session with the login Keychain available. Retain proof for probe/access-control state, CRUD/update, restart, concurrency, delete, locked/interaction-denied behavior, and non-disclosure. Refresh the A04 evidence report and artifacts before re-review. |
| Blocking / Critical | SEC-008, SEC-013 | Rollback is not interruption-safe or idempotent despite the evidence report's claim. `RollbackAsync` calls `RestoreSourceReferenceAsync` while the journal still says `ReferenceCommitted`, and records `RolledBack` only afterward (`SecretMigration.cs:221-245`). `RestoreSourceReferenceAsync` changes and saves the database reference first, then verifies the source (`SecretMigration.cs:515-533`). If source verification fails after the save, or the process stops after the save and before the journal update, the database already contains `SourcePayload` while the journal still says `ReferenceCommitted`. Retry then requires the database to contain the destination reference and throws `rollback-reference-mismatch` (`SecretMigration.cs:525-528`). Existing interruption coverage fails destination verification before commit and performs an uninterrupted rollback; it does not exercise either rollback boundary (`SecretMigrationTests.cs:118-156`). | Verify source readability before publishing the restored reference; make restoration recognize and verify the already-restored source as an idempotent resume state; and add deterministic failure injection after the database save and before journal advancement, plus failure during source verification. Prove retry, rollback, restart, source preservation, destination cleanup, and redacted audit behavior for legacy Data Protection and vault-reference sources on the applicable actual hosts. |
| Blocking / High | SEC-011, A04-T10 | The retained scanner does not satisfy its own non-disclosure guarantee or the required seeded-sentinel command. `scan_artifacts_for_secrets.py` stores a transformed copy of the whole matching line as `redacted_excerpt` (`scan_artifacts_for_secrets.py:97-109,157-165`), but its redactor does not cover a generic `secret:` field. The authoritative report therefore contains the unredacted synthetic values `spoofed-leaf-secret` and `array-secret` while claiming that secret values are never stored (`A04-secret-scan-final.json:13,77,93`). These fixtures are not production credentials, but they constructively prove that a sibling secret can be copied into scanner output. In addition, A04 validation mandates `--sentinel-file <private-test-input>` (`validation.md:12`), while the parser exposes no such option (`scan_artifacts_for_secrets.py:60-73`); the reported four-sentinel check is therefore an unrecorded separate search rather than the required scanner contract. | Make findings metadata-only or redact every excerpt without reproducing source text. Implement the private sentinel-file input without echoing values, fingerprint exact sentinel matches, test multiple secret-bearing values on one line, and record the effective size/exclusion settings. Regenerate and rescan every retained artifact; require zero seeded sentinel matches and no literal secret fixture in scanner output. |

## Architecture and provider assessment

No new project-reference reversal or project cycle was found. Independent inspection of
CodeAnalytics snapshot `snap-20260809183014-b07bdd50` confirms four scoped projects,
675 types, 4,487 members, 69 registrations, 190 findings, 13 diagnostics, and zero Error
findings/diagnostics. Project edges remain Infrastructure consumed by Modules.Security,
with Composition/Web composing them. The reported two-node Infrastructure module cycle
is pre-existing and intra-project.

The reviewed provider boundaries otherwise follow the intended direction. Auto maps
only to DPAPI, Keychain, or Secret Service and the first registered hosted validator
probes capability before later runtime-module hosted services start. Unix headless use
requires the explicit external wrapping-key provider; development file/in-memory
providers require both Development and explicit opt-in. Linux passes values through
standard input with `UseShellExecute=false`, `ArgumentList`, bounded waiting, and typed
non-secret remediation. The external provider uses AES-256-GCM with key identity in
associated data and current/previous key generations. Data Protection bootstrap uses
DPAPI or an independently supplied PFX, and the private XML repository uses atomic
create-new persistence; no vault dependency on the protected ring was found.

Those positive findings do not override the explicit host-proof condition or the two
constructive recovery/redaction failures above.

## Independent evidence checks

- Parsed the retained TRX counters: Windows full Unit `5,522/5,522`, security Unit
  `35/35`, and security integration `4/4`; Linux security Unit `35/35`, portable
  security integration `4/4`, and actual Secret Service `1/1`. All have zero failed or
  unexecuted results. The macOS integration test is present, but it returns immediately
  unless running on macOS with `CANDOITALL_KEYCHAIN_INTEGRATION=1` and no macOS TRX was
  supplied.
- The Windows and Linux Web Release logs end with successful Web output and contain no
  compiler warning/error finding. The logs do not retain an explicit build-summary or
  exit-code line, so the stated `0/0` result still depends on the executor's command
  status.
- The actual Linux artifact exercises one D-Bus/GNOME Keyring Secret Service session;
  this is valid Linux provider evidence but provides no macOS coverage.
- Parsed the secret scan: ten files, 12 findings, six duplicated synthetic fixture
  fingerprints. Literal A04 runtime/migration sentinels are absent from retained
  artifacts as reported, but the scanner-output defect above prevents SEC-011 closure.
- `git diff --check` produced no whitespace error; only the two recorded line-ending
  notices appeared.
- Independently reran `python scripts/validate_bundle.py --bundle-root .
  --skip-checksums`: 286 files, zero errors, zero warnings.

## Closure decision and residuals

Gate C2 remains open. Re-review is required after all three blockers are remediated and
fresh affected actual-host, rollback failure-injection, redaction, build, static, and
portable-validator evidence is frozen. The primary executor must regenerate the bundle
index/checksums and run checksum-enforcing validation only after review text and
remediation evidence are final.

Residuals not independently blocking this decision:

- Linux production still depends on an operator-provided D-Bus session, Secret Service,
  and unlocked keyring; the implementation fails closed when those are absent.
- External wrapping-key and PFX confidentiality, backup, and rotation remain deployment
  responsibilities. `X509KeyStorageFlags.Exportable` increases in-process key export
  capability and should be removed unless a documented runtime need requires it.
- The existing Infrastructure intra-project module cycle and large complexity hotspots
  remain cleanup inputs; neither creates a new A04 dependency-direction violation.

## Bounded remediation re-review

### Decision

**NO-GO — Security Gate C2 remains blocked solely by SEC-002/A04-T11 actual-macOS
proof.**

The SEC-008/SEC-013 rollback finding and SEC-011 scanner finding are closed. No new
blocking issue was found within the bounded remediation scope. A05 must remain blocked
until the existing Keychain integration passes on a genuine macOS user session and the
required access-control evidence is retained; Linux Docker and injected-native tests do
not satisfy that condition.

### Closed — SEC-008 and SEC-013 rollback recovery

- `RestoreSourceReferenceAsync` now resolves and verifies the source before opening and
  mutating the database record (`SecretMigration.cs:569-593`). An unreadable legacy Data
  Protection source therefore leaves the committed destination reference published.
- The method recognizes an already-restored `SourcePayload` and returns an idempotent
  resume result. A stop after the database save no longer causes
  `rollback-reference-mismatch` on retry.
- `RollbackAsync` invokes the deterministic `AfterSourceReferenceRestored` observer
  after the save but before destination deletion and journal advancement
  (`SecretMigration.cs:242-299`). The failure path persists only the typed
  `rollback-operation-failed` code and emits a typed audit event. Retry re-verifies the
  source, deletes the destination, and advances the journal to `RolledBack`.
- The new unit regressions prove post-save interruption, preserved source and staged
  destination during the interruption, retry cleanup, already-restored reference
  handling, pre-publication legacy-source verification, and absence of the seeded value
  from serialized audit events (`SecretMigrationTests.cs:165-281`). The recording-vault
  assertion proves destination cleanup after retry.
- The refreshed actual-Windows integration uses a real DPAPI source and durable
  external-wrapping-key destination, injects the post-save interruption, retries
  rollback, preserves the DPAPI source, then separately proves forward resume,
  checkpoint cleanup, and restart readability
  (`SecretPortabilityIntegrationTests.cs:58-161`). Its authoritative TRX contains the
  named test with outcome `Passed` on `LUCYSPOWER`.
- Independent no-build reruns passed the two exact rollback unit tests `2/2` and the
  exact Windows DPAPI integration `1/1`.

This closes the original interruption window. The actual-Windows test does not directly
assert absence of the old destination key after rollback, but the unit regression does,
the same production deletion path is used, and the integration exercises the real
durable destination's delete path without error. This is a minor evidence-shape
residual, not a remaining correctness blocker.

### Closed — SEC-011 scanner non-disclosure

- Scanner schema 2 removes source excerpts entirely. Each finding now contains only
  generated id, relative path, line, rule, and truncated fingerprint
  (`scan_artifacts_for_secrets.py:175-215`). The prior adjacent-value disclosure channel
  is gone.
- `--sentinel-file` is repeatable, loads unique non-empty UTF-8 values, excludes the
  private input paths from the scan, reports only fingerprints, and records file/value
  counts, sentinel findings, `max_file_bytes`, and excluded directories
  (`scan_artifacts_for_secrets.py:60-80,104-119,145-215`).
- Independently ran the scanner tests: metadata-only multiple-secret-line behavior and
  private-sentinel fingerprinting/non-disclosure both passed `2/2`. The tests also prove
  that neither scanner JSON nor console output contains the sentinel.
- Parsed `A04-secret-scan-final.json`: schema 2; ten files scanned with a 12,000,000-byte
  limit and no excluded directory; one private input containing four sentinels loaded;
  24 generic findings across six classified synthetic fingerprints; zero sentinel
  findings; zero excerpt fields. Finding keys are exactly `id`, `path`, `line`, `rule`,
  and `fingerprint`. The previously exposed synthetic adjacent values are absent.

The private sentinel values are correctly not retained, so an independent reviewer
cannot reconstruct their content from the evidence package. The repeatable input
contract, counts, zero-result, metadata schema, and executable tests provide adequate
non-disclosing proof for this gate.

### Refreshed evidence and consistency

- Parsed authoritative TRX counters: Windows full Unit `5,524/5,524`, focused security
  Unit `94/94`, and security integration `4/4`; Linux focused security Unit `94/94`,
  portable security integration `4/4`, and actual Secret Service `1/1`. Every artifact
  has zero failed and zero unexecuted results.
- Queried CodeAnalytics snapshot `snap-20260809191620-b07bdd50`: four projects, 678
  types, 4,490 members, 69 registrations, 190 findings, 13 diagnostics, and zero Error
  findings/diagnostics. Project direction is unchanged; the one reported module cycle
  remains the recorded pre-existing Infrastructure cycle.
- The refreshed Windows/Linux Web logs contain no warning/error hit and end with the Web
  output. They still do not embed the command exit code; the evidence report's exit-zero
  statement therefore depends on the executor's captured process result.
- Independently reran `git diff --check`: no whitespace error, with only the two recorded
  line-ending notices.
- Independently reran portable validation with deferred checksum enforcement: 289 files,
  zero errors, zero warnings.
- The evidence report, root README, execution report, requirement traceability, gate
  log, task status, and exit checklist consistently keep C2 at NO-GO and A05 blocked by
  genuine macOS Keychain proof. Their statements that the two local findings await this
  bounded re-review should be changed to closed during post-review bookkeeping; that
  expected synchronization does not change the remaining gate result.

### Exact remaining blocker

SEC-002 acceptance requires Keychain create/read/update/delete/restart/concurrency and
access-control tests to run on macOS. A04-T11 requires actual Windows, Linux, macOS, and
headless evidence before platform composition continues. No genuine macOS TRX or
equivalent retained host evidence exists. Therefore C2 remains **NO-GO solely for
SEC-002/A04-T11**, and A05 remains ineligible.
