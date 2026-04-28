# CanDoItAll MAF Round 5: Process Agent Operations, Recovery, Escalations, UI Control, and Testability Bundle

Date: 2026-04-28

This execution-grade bundle is based on the actual uploaded repository snapshot, not only on the pasted Codex report.

## Stop-the-line finding

The actual snapshot still contains a real-looking OpenAI API key pattern in `src/CanDoItAll.Web/appsettings.json`. Do not copy the value into logs, reports, tests, screenshots, or future bundles. Treat it as compromised and rotate/revoke it outside the repository.

The pasted Codex report claims an execution report and several new recovery/secret-scanning files exist. In the actual snapshot, the expected round-4 report and several claimed classes/tests were not found. This bundle therefore starts with snapshot integrity and secret handling before any architectural work.

## Primary objective

Make the process module and MAF agent runtime safe and operable in production-like conditions:

- no committed secrets,
- no false implementation reports,
- process mutation tools governed as mutations,
- structured/finalized outputs enforced end-to-end,
- agent failures converted into typed recovery or rework decisions,
- QA returns handled as targeted rework rather than blind reruns,
- proof receipts reused only when fingerprints are still valid,
- escalations and approvals surfaced as a first-class control plane,
- process UI capable of monitoring, triaging, approving, rejecting, and initiating rework,
- runtime code decomposed into testable services,
- stable test gates that prove the above behavior.

## Required execution order

1. Complete `subbundles/00-snapshot-integrity-secret-stop-the-line` first.
2. Complete governance/runtime safety subbundles 01-03.
3. Complete recovery/rework subbundles 04-06.
4. Complete escalation/UI/observability subbundles 07-09.
5. Complete architecture/testability subbundles 10-12.
6. Produce the required execution report and run the readiness gate.

Do not claim completion unless the actual files and tests exist in the repository snapshot.
