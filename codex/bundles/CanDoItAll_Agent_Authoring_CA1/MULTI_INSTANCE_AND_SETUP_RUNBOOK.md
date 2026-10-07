# Owned native fixtures and bounded external execution

Reuse current SharedProviders tooling rather than introducing a second orchestration stack.
Inspect the current runner parameters and ownership checks; do not paste an old Reset command.
Preserve S0 and PP2/PP3 evidence and existing retained instances. Create a unique marker/run ID,
Compose project, nonconflicting loopback ports, per-instance control-plane/storage and separate
PostgreSQL databases. Fingerprint executable main/Components/FileTools/upstream inputs and image
digests. Post-extraction proof must use rebuilt affected binaries/images, not the prior PP3 image.

The primary topology remains source + client A + client B with deterministic upstream and isolated
stores. Only intended network paths exist; keep no-direct-fallback controls. Credentials are newly
issued for the owned fixture only, short-lived and least-scoped. Upstream credentials stay at the
source. Do not persist tokens in the handoff or generic test report. Capture shared model ID versus
source display/upstream identity separately. Run relevant native defaults/shared consumer checks;
full protocol rerun is justified by changed runtime/transport/fixture inputs, not every markup edit.

For setup tests, provision explicitly owned fixtures:
- HTTP endpoint returning bounded JSON and an independent invocation counter.
- Process helper in a task-owned root, allow-listed executable, deterministic input/output, bounded
  timeout/bytes and no access to ordinary workspace. Do not launch arbitrary user commands.
- Supported MCP server with known tools, owned stdio/HTTP transport, start/list-tools counter and
  bounded lifecycle. A missing production registration is not replaced by a fake green success.
- File/inline/upload skill fixtures with known root, invalid/outside-root negatives and no automatic
  script execution on display. Secret binding tests use synthetic locally created secret references.

Disallowed attempts must not dispatch. A canceled/timed-out attempt after dispatch may have an
effect: preserve native classification. Stopping a dialog cannot certify rollback. Do not copy
user's real tokens/keys, install packages from an untrusted submitted skill or attach host directories
beyond owned fixtures. No email/payment/deployment operations.

Use an explicitly isolated PostgreSQL server required by current tests. PP2 previously filled a
1 GB tmpfs; budget storage based on actual suite volume and monitor free space. Increasing owned
fixture capacity does not authorize changing application SQL/HTTP deadlines. Use --init in Linux
process-test containers where required. Collect before cleanup, retain failures and safe logs,
then stop only containers/processes matched by owned IDs/labels/markers. Never global prune/kill.

Record numeric streaming latency on a relevant protocol run, including first payload and terminal
observations and bound. An old Boolean-only failure remains unexplained despite a passing resume.
No paid calls or reset of the old 40/40 budget. Current local deterministic tests have independent
bounded request/process counts, recorded before execution and checked afterward.
