# Assumptions And Risks

## Assumptions
- Codex runs on branch `maf-processes-refactor`.
- The latest bundle `process-driver-multi-domain-verification-gateway-v1` is present and marked completed.
- The repo intentionally supports .NET 10.
- The driver packages are alpha verification-only packages and are not yet runtime-integrated.

## Critical Path Risks
- A gateway expansion could accidentally become a dynamic runtime host.
- Process module adapters for domain drivers could hide process mutation behind read-only names.
- Historical architecture fixture skips could remain permanent and hide real regressions.
- Driver contract versioning could drift without migration notes and API hash updates.
- Evidence URI/hash policy could diverge between transcript/runtime/artifact/Office/business drivers.
- Office and business-analysis lanes could accidentally imply Graph/connector/business-record mutation.
- Observation aggregation could become persistence or event emission instead of read-only aggregation.
- New proof rows could be report-only without real source/test evidence.

## Validation Risks
- Full build is not enough. Driver packages need focused unit tests and source scans.
- Full unit tests with skips are not enough unless every skip has an owner, reason, and reopen trigger.
- Gateway tests must reject generic `Verify(lane, object payload)` style dispatch.
- Adapter tests must prove no file IO, network IO, process mutation, workspace/storage writes, DI registration, scheduler hook, workflow hook, or manager command.
- Redaction tests must verify diagnostics, audit summaries, and evidence metadata do not leak secrets or emails.

## Reopen Triggers
- Any new driver package references Modules, Infrastructure, AgentFramework, EF, workspace/storage, UI, or connector packages.
- Process Core references any driver abstraction or implementation package.
- Process module gains a driver registry, selector, manager command, DI registration, scheduler hook, workflow hook, or generic runtime host.
- Full unit skips increase or stale fixture skips remain without a current source-backed replacement.
- Any proof manifest lacks changed-file hashes, command transcript paths, source assertions, semantic positive proof, adversarial negative proof, and anti-stub audit.
