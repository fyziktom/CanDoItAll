# Preparation-package review

**Status: PREPARATION_REVIEW_PASSED**  
Reviewed: 2026-09-23. This verdict applies only to the instruction package, not to CanDoItAll implementation or runtime validation.

## Substantive review and corrections

- Rechecked development HEAD after the source audit: `ffa83cf903c305a7490a674f41a0d08498156db6`, still the PostgreSQL 18 merge. The SHA remains provenance, never an execution pin.
- Corrected the historical baseline to MAF 1.20.0 and verified the 1.22 stable/A2A preview target. Included the actual AI, general Extensions, OpenAI and OpenTelemetry dependency floors rather than prescribing a two-property bump alone.
- Reconciled the earlier incomplete release summary: the matrix includes approval replay, shared session-store, provider-backed declarative MCP and sticky Foundry-identity changes. Conditional upstream APIs are separated from the actual imperative workflow and application-owned MCP paths.
- Compared native approval state, durable application approvals and journal recovery requirements. Required genuine pre-upgrade fixtures and explicit compatibility outcomes; removed any assumption that matching major versions prove safe continuation.
- Separated three source-observed defect candidates (F01–F03), the mandatory compatibility review (F04), maintained-documentation drift (F05), and six unproven hypotheses. Production escalation causation is not asserted.
- Checked alignment of process recovery, unknown side effects, permanent failures, finalizers, child-work reuse, budgets and cancellation. No requirement permits weakening approval or retry safety to reduce escalation counts.
- Reconciled MCP-first ownership investigation with actual-diff impacted-test analysis. Added explicit blocked-gate handling for an unavailable MCP without treating text search as equivalent analysis.
- Checked the full-suite definition against the current repository guide: unfiltered Stable and Playwright on actual Windows and Linux; standard automated proof before browser execution. Added measured interruption handling and a narrowly evidenced documentation-only final-diff exception to avoid pointless runtime reruns.
- Clarified that the existing workflow-backed process dispatch cannot be dismissed as unsupported merely because a convenient UI fixture is absent.
- Checked current conversation-shell ownership, genuine project-scoped UI actions, deterministic versus real-provider proof, retained-state/rollback notes, source references, traceability and reporting. The execution and result templates intentionally remain NOT_RUN.

## Mechanical checks performed

The preparation validator passed UTF-8/JSON, required-file, local-link, fence-balance, source-ID, traceability and manifest consistency checks. The reviewed package contains 42 primary-source entries and 33 traceability rows. Eight isolated negative tests also passed: altered content, missing required artifact, unlisted artifact, broken relative link despite resealing, malformed JSON despite resealing, falsely passed execution template, root-escaping manifest path, and missing mandatory traceability row were each rejected.

The final archive is checked for ZIP CRC integrity, extracted into a disposable directory, and passed through the same validator before delivery. No original repository checkout or production data is modified by these preparation checks.

## Scope and evidence limits

The audit covers selected relevant source and test inventories, not every line of the repository. Large files were sometimes inspected in relevant ranges; individual source entries state their scope. Search results from the default branch were used as locators and current development files were fetched against the audit SHA for substantive findings. Some upstream PRs are represented by release membership plus tagged implementation rather than a full diff review.

No CanDoItAll restore/build, CodeAnalytics MCP query, production regression, Windows/Linux suite, browser journey or live-provider experiment was executed in preparing this pack. No production escalation root cause is claimed reproduced or resolved. Codex must establish all implementation acceptance evidence in a separate output directory.

`MANIFEST.sha256` and `tools/validate_pack.py` are integrity aids, not cryptographic publisher authentication, external-link validation or a substitute for product tests. The validator requires Python 3.10 or newer.
