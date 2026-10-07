# Review and prioritization

## Decision

Preserve the completed Workspace leaf architecture. Do not launch the next module yet.
Source review agrees with the implementer's separation of structural UI completion and
failed composed-application readiness. Three reported shared lifetime failures require
actual repairs, and one additional exact-profile edit race is demonstrated by the source
control flow. No runtime reproduction was executed by this reviewer. See [sources](SOURCES.md).

| ID | Nature and priority | Current conclusion |
|---|---|---|
| WCL-R1 | New source finding; high functional correctness priority | Successful preflight is separated from a legacy upsert; a concurrently deleted saved profile can be recreated under its old ID. Add failing-first owner proof and an atomic exact-editor admission. |
| WC-C1 | Reported recurring shared lifecycle failure, priority P2 in maintained record | Simple Chat query/runner can outlive disposed services and semaphore; source confirms missing ownership coordination. Exact scope/disposal ordering still needs controlled isolation. |
| WC-C2 | Reported recurring BaseLib teardown failure, priority P2 | Cancelled JS close bypasses later module/.NET cleanup; source confirms the unprotected sequence. Fix shared dependency and consumer delivery. |
| WC-C3 | Reported recurring shell/browser-state failure, priority P2 | First-render/queued navigation continue through disconnected browser persistence; no global layout lifetime in the read source. Preserve active save semantics. |
| WC-L1 | Live positive file-path proof gap and harness defects | Real workspace write progress was observed, but no full positive attach/read-back/UI proof. Negative control passed. Do not claim rollback. |
| WC-L2 | Live Workflow proof gap; root cause unconfirmed | Temperature rejection is known; later HTTP success with failed provider/Workflow state is not a successful model result. Small output cap is only a hypothesis. |
| WCL-V1 | New harness hardening requirement, not a demonstrated app exploit | Approval loop checks tool names and approval identity but does not explicitly validate all expected fixture arguments before deciding. Add semantic target validation and reject unexpected proposals. |

Severity means closure priority. This review does not establish data exfiltration, a permission
bypass, a production outage, or that every failure was introduced by the UI refactor.
WC-C1/C2/C3 have uncertain historical attribution. WCL-R1 crosses a new seam into an older
upsert; it must be fixed for the new exact-editor contract regardless of when it originated.

## What is preserved

Recovery maps the five explicit actions to existing owners, carrying the original context,
intent and exact Workflow occurrence/slot/fingerprint. It retains an acknowledged result
before optional refresh. Its sessions cannot publish into a disposed successor. (R15, R16)

Data Sources has distinct runtime/list/editor/schema reads, stable editor identity and a
password intent separate from retained receipts. The initialization revision guard is in
current source. Transfer captures source, target, group keys and replacement choice; the
ledger is circuit-local, not a new durable recovery service. These are sound boundaries,
not a reason for a broad redesign. (R09, R12-R14)

The max-output-token handoff now reaches the neutral LLM invocation request in
WorkflowLlmComponentInvoker. Preserve this actual fix and its deterministic runtime test;
its presence does not imply that a particular live model call completed. (R28, R29)

The census deliberately keeps route, DI, trust-registry and compatibility projection hosts
in the product module. A legitimate host remaining in Workspace is not unfinished extraction.
Re-audit actual render descendants and dynamic registrations instead of requiring zero
Razor files or zero pre-existing references. (R03, R04)

## Review limits

This was connected pinned-source/metadata review plus examination of maintained test reports.
No .NET SDK was available in the reviewer environment. No product build, app test, browser,
watch benchmark or controlled C# reproduction was run. Original ignored TRX/logs/screenshots
were unavailable to this reviewer. The supplied preceding archive contains instructions,
not the raw output of the implementation. Package tests verify this handoff only.

The closure must obtain fresh proof for corrected code. A report citing a SHA-256 of an
unavailable log is useful provenance, but not independent inspection of that log.
