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

## SEC-014 bounded remediation re-review

### Decision

**NO-GO for SEC-014. Security Gate C2 remains NO-GO.**

The reported Development startup contradiction is corrected, and the database
InMemory fingerprint disclosure is fixed. However, three product/security findings and
one evidence gap prevent SEC-014 closure. Independently rerun focused tests passed
`4/4`; this does not override the contract conflicts below. No genuine macOS evidence
has appeared, so SEC-002/A04-T11 remains an additional independent C2 blocker and A05
must stay blocked.

### Blocking findings

1. **P0 — `LocalUserFile` violates the still-binding SEC-007 production/Auto
   boundary.** `SecretVaultFactory` guards only `DataProtectionFile` and `InMemory` by
   environment/opt-in, while `LocalUserFile` is accepted in every environment and
   non-Windows `Auto` selects it (`SecretVaults.cs:161-208`). `LocalUserFileVault` then
   delegates unchanged to `DataProtectionFileVault`, whose `vault.key` is the Base64
   encoding of the raw AES key stored beside the ciphertext
   (`SecretVaults.cs:295-298,362-383,397-439`). SEC-007 still requires that exact
   Base64-key design to be absent from production paths and never selected by Auto
   (`requirements/requirements.json:318-324`). Adding a truthful warning does not
   satisfy that requirement. Resolve the contract deliberately: the smallest coherent
   implementation is Development-only basic-local Auto/explicit use, with production
   non-Windows Auto retaining the strong-provider/fail-fast policy; alternatively, a
   security-authorized requirement/ADR change and a different production threat model
   are needed. Renaming the same mechanism is not a valid closure.

2. **P0 — Windows “user-private, permission-hardened” availability is neither enforced
   nor proved.** `DurableFileWriteOptions.Private` requests only
   `RequirePrivateUnixMode`; both directory and file hardening are explicit no-ops on
   Windows (`DurableFileWriter.cs:23-35,538-568`). The vault accepts any absolute
   configured `VaultPath`, and `LocalUserFileVault.ProbeAsync` unconditionally reports
   `Available/BasicLocal` without checking that the directory is writable or restricted
   to the current user (`SecretVaults.cs:424-430,632-650`). The only local-vault mode
   assertions are Unix-only (`SecretPortabilityTests.cs:129-139`). Thus the startup
   warning's “user-private” statement and SEC-014's Windows permission-hardening
   acceptance are not established. Restrict the Windows store to a demonstrably
   per-user root or apply and verify owner-only ACLs, make the capability probe fail
   with non-secret remediation when the store cannot meet that policy, and retain an
   actual-Windows ACL/startup/restart regression.

3. **P1 security correctness — the new protection taxonomy falsely marks the
   development-only in-memory secret vault as `Strong`.** The helper defaults every
   available result to `Strong` (`SecretVaults.cs:54-63`), and
   `InMemorySecretVault.ProbeAsync` accepts that default (`SecretVaults.cs:467-476`),
   even though the factory classifies that provider with the insecure-development
   policy (`SecretVaults.cs:167-174`). The startup validator consequently emits no
   weaker-provider warning. Give ephemeral/insecure development storage an explicit
   truthful level (or at minimum non-Strong state and notice) and cover it through the
   factory/startup validator.

4. **P1 evidence gap — the retained remediation artifact set was not completely
   scanned.** `A04-remediation-2-secret-scan-authoritative.json` reports a 5,242,880-byte
   limit and 16 scanned files. The scanner silently skips larger inputs
   (`scan_artifacts_for_secrets.py:83-92,154-166`), including both 766–839 MB final and
   authoritative build logs and both 8.3 MB full Windows TRXs; the source tar and patch
   are also outside its text candidate set. The classification nevertheless says the
   build logs have zero findings, and the report generalizes this to retained evidence.
   My bounded exact-pattern stream check found no `must-not-leak`, `Password=`, seeded
   vault sentinel, or PEM private-key marker in the two authoritative build logs, but
   the private sentinel inputs are intentionally unavailable and this is not equivalent
   to the required scanner proof. Use a streaming/chunked scanner or retain compact
   build summaries; report scanned, skipped, and excluded files explicitly, then rerun
   with private sentinels over every retained text artifact.

### Confirmed remediation behavior and evidence

- The checked-in Development profile now selects `LocalUserFile` directly without the
  legacy opt-in. Both authoritative startup logs contain the typed `LocalUserFile /
  BasicLocal` warning, reach `Application started`, and have empty stderr. The logs do
  not themselves retain the claimed HTTP 200 response, so that detail remains an
  executor observation rather than independently reconstructable evidence.
- The provider preserves the legacy directory, key, payload naming, and AES-GCM format.
  The cross-platform regression reads legacy-written content, writes new content,
  recreates the provider, rereads it, excludes plaintext values, and proves `0700/0600`
  on Unix. Raw `DataProtectionFile` remains production-rejected and read/delete-only
  through the migration-source boundary; authorized Development compatibility exposes
  the `DataProtectionFile` name with a `BasicLocal` warning.
- Windows Auto still selects DPAPI. Explicit DPAPI, Keychain, Secret Service, and
  external-wrapping-key constructors/probes remain fail-closed; no runtime downgrade
  from an explicitly selected strong provider was introduced. Non-Windows Auto's new
  basic-local production behavior is the SEC-007 conflict above, not an explicit
  provider fallback.
- `InMemoryDatabaseIdentity` now rejects inherited external-connection syntax as an
  in-memory database name and uses an opaque truncated SHA-256 identity. The regression
  proves that the seeded PostgreSQL connection does not enter the resolved name,
  workspace identity, startup connection, or fingerprint. The scanner finding that
  motivated this correction is closed at product level.
- Parsed authoritative counters are Windows focused Unit `97/97`, full Unit
  `5,527/5,527`, and integration `4/4`; Linux focused Unit `97/97`, secret portability
  `3/3`, and migration `1/1`, all with zero failed/unexecuted results. The four exact
  SEC-014/fingerprint tests independently passed `4/4` with `--no-build --no-restore`.
- `git diff --check` produced no whitespace error and one recorded line-ending notice.
  Portable validation with deferred checksums passed at 290 files, zero errors, zero
  warnings.

### Evidence consistency and required bookkeeping

The main evidence report is internally stale: its opening design/result text still says
Auto selects macOS Keychain/Linux Secret Service and that Auto/production reject the
file vault (`reviews/13-a04-evidence-report.md:15,19,34`), while its SEC-014 section and
current source say non-Windows Auto selects `LocalUserFile`
(`reviews/13-a04-evidence-report.md:93-128`). This mismatch is material because it hides
the SEC-007 conflict; it is not mere post-review checkbox bookkeeping.

After product policy, Windows privacy, taxonomy, and complete redaction proof are fixed,
the executor should refresh both-host focused/full/startup/build evidence and append a
bounded independent re-review. Only after GO should canonical bookkeeping mark SEC-014
and A04-T12 review complete and synchronize the evidence report, requirement
traceability/source manifest, A04 README/tasks/validation/exit criteria, execution
report, gate log, root status, and A04 handoff. C2 and A05 must still remain blocked for
the genuine macOS SEC-002/A04-T11 proof. Finally regenerate the bundle index/checksums
and run checksum-enforcing portable validation after all review and bookkeeping text is
final.

## SEC-014 second bounded remediation re-review

### Decision

**GO for SEC-014. Security Gate C2 remains NO-GO solely for genuine macOS
SEC-002/A04-T11 proof.**

All four SEC-014 blockers from the preceding review are closed. The amended policy is
explicit and internally coherent, actual Windows and Linux evidence matches it, and
the schema-3 redaction proof completely accounts for the retained remediation
artifacts. No new blocker was found in the bounded scope. A05 remains blocked because
no genuine macOS Keychain execution evidence exists; Docker and early-return/injected
tests do not satisfy that independent condition.

### Closed — SEC-007 and platform baseline policy

- SEC-007 now distinguishes the legacy provider *name* from the deliberately supported
  Unix storage tier. `DataProtectionFile` remains Development/migration-only, while
  Unix `LocalUserFile` is expressly authorized as `BasicLocal` under its documented
  same-user threat model (`requirements/requirements.json:318-324`). This removes the
  earlier direct contract violation rather than merely relabeling it.
- ADR-C08 records the security decision and rejected alternatives: Windows Auto uses
  current-user DPAPI/`Strong`; Unix Auto uses AES-256-GCM `LocalUserFile` with enforced
  `0700/0600`, `BasicLocal`, and an explicit warning that same-account code can read the
  colocated key; operators use Keychain, Secret Service, or an external wrapping key
  when same-user isolation is required (`architecture/07-adrs.md:35-41`). ADR-C04 still
  forbids fallback after an explicit strong provider is selected.
- The policy is a defensible local-install baseline, not a claim of OS-vault-equivalent
  protection. `LocalUserFile` protects against casual/offline disclosure subject to
  owner-only Unix permissions, but deliberately does not protect against compromise of
  the owning account. The typed capability and warning expose that distinction.
- Factory selection matches the policy: Windows Auto returns DPAPI; non-Windows Auto
  returns `LocalUserFile`; explicit DPAPI, Keychain, Secret Service, and external-key
  providers retain their existing probes and fail closed without switching provider
  (`SecretVaults.cs:155-217`). The Unix roundtrip/restart regression proves legacy and
  new payload continuity, absence of plaintext values, vault-root `0700`, and every
  retained vault file `0600` (`SecretPortabilityTests.cs:152-202`).

### Closed — Windows privacy claim and provider selection

- `SecretVaultFactory` now rejects explicit `LocalUserFile` on Windows with typed
  `UnsupportedPlatform` remediation directing the operator to Auto/DPAPI
  (`SecretVaults.cs:168-174`). The capability itself also reports unsupported on
  Windows, so a directly composed instance cannot pass startup validation
  (`SecretVaults.cs:435-444`).
- The Development configuration now selects `Auto`, not `LocalUserFile`, and has no
  insecure-provider opt-in (`appsettings.Development.json:7-9`). On Windows, Auto
  resolves to current-user DPAPI and its capability reports `Strong`; no Windows
  owner-only file-ACL claim remains for the Unix-only basic tier.
- The Windows v2 startup artifact reaches `Application started`, retains HTTP 200, has
  empty stderr, and emits no weaker-vault warning. The focused suite exercises Windows
  Auto-to-DPAPI, explicit LocalUserFile rejection, and DPAPI CRUD; the actual-Windows
  integration retains the DPAPI export/re-encryption/restart test. This is sufficient
  evidence for the Windows half of SEC-014 without relying on Unix-mode semantics.

### Closed — truthful Development-only capability

- `SecretVaultProtectionLevel` now has an explicit `DevelopmentOnly` member
  (`SecretVaults.cs:38-44`). An authorized legacy `DataProtectionFile` compatibility
  instance reports `DevelopmentOnly` with a legacy key-beside-ciphertext notice, while
  the raw migration implementation remains unavailable as a startup provider
  (`SecretVaults.cs:297-319,406-457`). Production factory use is still rejected.
- `InMemorySecretVault` reports `DevelopmentOnly` and warns that values are process-only
  and lost on restart (`SecretVaults.cs:494-508`). The startup validator warns for every
  available level other than `Strong`, so neither Development-only provider can be
  presented silently as strong (`SecretVaultStartupValidation.cs:37-50`).
- The new regression resolves InMemory through the guarded factory, checks the typed
  capability/notice, runs startup validation, observes a warning containing
  `DevelopmentOnly`, and proves the seeded sentinel is absent. The authorized legacy
  regression also proves existing payload readability and its distinct typed level.

### Closed — complete remediation artifact scan

- Scanner schema 3 records every candidate as scanned text, oversized text, excluded
  non-text, unreadable text, or control input. `.patch` is now a text candidate, and the
  report includes file names and sizes for every gap category
  (`scan_artifacts_for_secrets.py:17-32,84-92,138-185,220-254`). The Python regressions
  cover metadata-only output, private sentinel handling/non-disclosure, and explicit
  scanned/oversized/non-text accounting; my independent rerun passed `3/3`.
- Parsed `A04-remediation-2-secret-scan-sec014-v2.json`: schema 3; 37 candidates; 36
  scanned text files; the regenerating output JSON is the sole control exclusion; zero
  oversized, non-text, or unreadable files; one private sentinel input containing two
  values; zero sentinel findings. Finding records remain limited to id/path/line/rule/
  fingerprint metadata.
- All 72 generic occurrences are confined to Unit TRXs and collapse to six known
  synthetic negative-test fingerprints: 18 GitHub-token, 18 OpenAI-token, and 36
  secret-assignment occurrences. Independent grouping found no finding outside a TRX.
  Startup, HTTP, environment, integration, and compact build proof is therefore covered
  without the prior silent-size omission. The removed diagnostic logs, source archives,
  and source patch are not part of the current retained proof set.

### Independent evidence checks and residuals

- Parsed current counters: Windows focused Unit `99/99`, full Unit `5,529/5,529`, and
  integration `4/4`; Linux focused Unit `99/99`, portability `3/3`, and migration `1/1`.
  Every current TRX has zero failed and zero unexecuted results.
- Independently reran the five exact Windows provider/taxonomy tests with
  `--no-build --no-restore`: `5/5`. The scanner unit suite independently passed `3/3`.
- Both compact Web build logs contain the final Web assembly output and no warning,
  error, or failed-build hit. Both current startup stderr artifacts are empty; retained
  HTTP summaries report 200. The Linux environment summary proves Auto with no D-Bus
  session or external wrapping-key input and identifies the resolved
  `LocalUserFile/BasicLocal` profile.
- `git diff --check` produced no whitespace error and only the recorded traceability
  line-ending notice. Portable validation with deferred checksums passed at 290 files,
  zero errors, zero warnings.
- Accepted residual: Unix `LocalUserFile` cannot protect secrets from code running as
  the same OS account. That is the explicit ADR-C08 threat boundary, is surfaced at
  startup, and does not weaken explicitly selected strong profiles.
- Evidence-report bookkeeping remains: the opening design summary and SEC-007/SEC-011
  rows in `reviews/13-a04-evidence-report.md:15,34,38` still describe the pre-SEC-014
  Auto policy and schema-2 proof, while its later SEC-014 section contains the current
  policy/schema-3 evidence. Because the amended requirements, ADR-C08, implementation,
  current artifacts, and later report section agree, this stale summary is not a
  product/evidence blocker; it must be synchronized before index/checksum freeze.

### Exact remaining gate blocker and bookkeeping

SEC-002 acceptance and A04-T11 still require Keychain
create/read/update/delete/restart/concurrency and access-control proof on genuine macOS.
No macOS artifact is retained. Therefore overall C2 remains **NO-GO solely for
SEC-002/A04-T11**, and A05 remains ineligible.

Post-review bookkeeping should mark SEC-007/SEC-014 and A04-T12 independently verified,
update the stale evidence-report summary/table, and synchronize requirement
traceability/source manifest, A04 README/tasks/validation/exit criteria, execution
report, gate log, root status, and A04 handoff while preserving C2 NO-GO/A05 blocked for
macOS. Regenerate the bundle index/checksums and run checksum-enforcing portable
validation only after those edits and this review text are final.
