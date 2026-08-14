# B04 independent Gate R3a review

## Decision

`GO for Gate R3a.`

No blocking product, architecture, security, lifecycle, or evidence-integrity finding remains for MCP-001 through MCP-005 and TOOL-001 through TOOL-002 on the frozen local package. This decision does not claim actual macOS validation, hosted validation, an aggregate full-suite rerun, or final Gate R4.

## Independent findings

No blocking finding.

The issues identified during the held source review are closed in the final snapshot:

- Local MCP setup and invocation now share strict typed validation for provider targets, secret-source identifiers, environment names, ordinal tool identities, approval evidence, external working-directory authority, and secret-free argv. Duplicate or invalid bindings and provider collisions fail before secret resolution or process launch.
- The runtime enforces the configured tool allowlist before protocol output. JSON-RPC initialization requires version `2.0`, an object result, the supported protocol version, capabilities, and non-empty server information; tool arguments and call results are object-shaped. Numeric approval representations are rejected rather than being accepted as enum aliases.
- Agent-scoped external roots are resolved only through path authority and remain opaque in receipts. The UI and curator preserve exact argv/path values, use host-aware environment-name equality and ordinal tool equality, require per-launch approval where configured, and round-trip the corrected model without silently changing authorization semantics.
- Playwright resolution uses the controlled exact-version application root. Its evidence covers the complete managed-install content tree, entry type/mode, contained link targets, lockfile, Node identity, final executable target, no-replace publication, and tamper/conflict rejection. The package selector accepts convenience flags only before the package token and preserves post-package argv.
- Both the setup-test path and production invocation reject unauthorized tools and secret-bearing persisted argv. The local MCP session owns bounded initialization, request, cancellation, shutdown, residual cleanup, and pre-handoff failure cleanup through the canonical B01 process host.

## Architecture and dependency result

- B01 remains the single low-level process implementation. B04 MCP and external-tool production sources contain no divergent `Process.Start`, process enumeration, or process-tree implementation; `WorkspaceExternalProcessRunner` and local stdio MCP consume the canonical process host/session contracts.
- Composition registers the external runner, MCP client factory, and setup-test service at the consuming scope. The long-running host is an alias of the same canonical process-host instance within each composition model; no second lifecycle owner was introduced.
- MCP protocol/package/secret/path policies remain in their capability/application boundaries. Physical path resolution and executable authorization are delegated to the existing host-aware authorities, while secrets cross the runtime boundary only as delayed values after descriptor, package, approval, tool, and path validation.
- The independently recomputed repository graph contains 106 projects and 634 in-repository `ProjectReference` edges, with no missing target and no cyclic project. The B04 project-reference changes preserve inward dependency direction.

## Correctness and security result

- MCP-001: requested and final executable identities are authorized with host-aware suffix, case, permission, explicit-path, foreign-path, and final-link-target behavior. Actual Windows and Linux cases complement deterministic foreign-host fixtures without being represented as actual macOS proof.
- MCP-002: duplex stdio launch, initialization, exact argv/environment handling, caller cancellation, timeout, shutdown, tree cleanup, and residual rejection use one owned B01 session. No server session is handed off before initialization and authorization complete.
- MCP-003: no production global-cache discovery is authoritative. Managed Playwright reuse is contingent on complete content-tree and runtime identity verification; publication is atomic/no-replace and external links or modified dependency content fail closed.
- MCP-004: persisted configuration accepts secret references rather than raw values. Identifier and collision checks precede lookup, resolved values are bound only at invocation, inherited environment is cleared, and receipts/audit output retain approved names rather than values.
- MCP-005: command, runtime, package, path, permission, secret, platform, handshake/list, malformed protocol, start, timeout, cancellation, and cleanup failures remain typed and deterministic rather than collapsing into a misleading success or fallback.
- TOOL-001/TOOL-002: the external process adapter shares B01 timeout, output-limit, cancellation, and tree-termination behavior. JSON parse, valid non-object JSON, nonzero exit, cancellation, and cleanup diagnostics are bounded and do not copy stdout, stderr, environment values, or physical external roots into receipts or agent context.

## Evidence reconciliation

- Governed proof contains 29 failing-first/correction records and 16 passing semantic source assertions. I independently recomputed all 66 source hashes and all 16 test/build/host-artifact hashes (four TRXs, ten builds, and two host records): zero files were missing and zero hashes differed.
- All four TRXs have a completed result summary with total equal to executed and passed: Windows 154/154 unit plus 18/18 integration, and pinned Linux Docker 154/154 unit plus 18/18 integration. Failed, error, timeout, aborted, inconclusive, and not-executed counters are zero.
- The ten affected build logs contain no compiler warning/error diagnostic and no failed-build marker. The governed hashes bind the exact logs used by the report.
- The source-reference manifest reconciles to 115 records, 115 unique identifiers, 115 unique portable paths, and zero missing files.
- The schema-3 scan accounts for all 19 candidates as 18 scanned text artifacts and one excluded scanner control, with no oversized, non-text, or unreadable gap. Its four metadata-only findings share one fingerprint and are confined to the intentional synthetic secret-shaped argv regression repeated in result/definition sections of the two unit TRXs. The classification is specific and the scanner stores neither the captured value nor a source excerpt; no unclassified secret finding remains.
- The two host records identify the exact Windows environment and pinned Linux image digest, OS, runtime, architecture, commands/result shape, and deferred macOS boundary without exposing secret values or physical purpose roots.
- `git diff --check` exits successfully and emits only the three recorded traceability-CSV CRLF notices. The portable runtime validator independently passed before this review file was added with 326 files, zero errors, and zero warnings using `--skip-checksums`.

No broad/full suite or broad build was rerun during this independent pass.

## Residual risks and deferred proof

- Genuine macOS execution remains operator-deferred. Deterministic macOS/path fixtures are not actual-host proof; a later macOS failure must reopen B04 and affected downstream gates.
- Hosted execution, a new aggregate full-suite run, and final Gate R4 remain deferred. This local R3a decision cannot be used as a hosted-support or final-release claim.
- The managed Playwright digest is a same-install integrity mechanism, not an external code-signing trust anchor. Same-account hostile filesystem mutation during use remains an operational threat boundary; detected tamper/conflict deliberately requires repair rather than silent replacement.
- Network/package-registry availability remains an installation-time dependency. The no-network reuse path is proven only for an already valid controlled installation.
- Remote HTTP-tool response hardening is outside TOOL-002's local-process stdout/stderr scope and is not claimed by this decision.
- CodeAnalytics export was unavailable under the recorded security boundary. Independent project-graph recomputation, source assertions, direct boundary inspection, hashes, and focused executable evidence provide the local substitute; the limitation must remain visible rather than being described as a CodeAnalytics pass.

After this review, the executor must update the canonical R3a/status records, regenerate the bundle index and checksums, and rerun the final portable validator. B05 becomes eligible only after that integrity bookkeeping. This review does not itself advance B05.
