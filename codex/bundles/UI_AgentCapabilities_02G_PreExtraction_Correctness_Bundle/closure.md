# Capabilities-02G bounded closure

Status: CLOSED

Closed UTC: 2026-09-07T02:46:00.267123+00:00

Reference: CDA-UI-SEAMS-AGENT-CAPABILITIES-02G. This closes the correctness prerequisite only; Capabilities-03 must still collect its real pre-extraction baseline before movement. Source basis: b540b465cf408dd9c9adeb939538b78ced9bc7bb plus the owned unstaged source/test inventory and patch. Local and independently read remote HEAD still match; index is untouched; both live siblings remain clean and unchanged. No commit, push or history operation was performed.

All A0-A9 findings are adjudicated in [the review](adjudication.md). The additional lease-copy defect and the two configuration-revision producers were proven before narrow correction. [Semantic proof](semantic-test-map.md), [validation](validation.md), [post-push receipt](post-push-receipt.md), [architecture scope](proof/architecture-analysis.json) and [source inventory](proof/source-inventory.json) provide the exact basis.

Accepted configuration revisions now advance strictly beyond their prior value even for equal/backward clocks, imported future timestamps or older proof observations. Observation time remains separate. Rejected/superseded operations do not advance revisions; unchanged normalization is idempotent. No schema or second version system was introduced.

Escaping command failures finish only their own retained operation as non-active Unconfirmed. Receipt-backed verification and assignment retain canonical read-only recovery. Receipt-less diagnostics allow exact-attempt explicit acknowledgement that they may have executed; the next diagnostic requires new intent. Curator unknown acknowledgement likewise releases only admission after managed-chat inspection, retaining any known returned chat and performing no launch/deletion. These are circuit-scoped paths, not durable outcome proof.

Final provider capture uses actual current catalog revision. An immutable lease preserves its fingerprint rather than losing it through JSON copying. Initial infrastructure/composition failures become typed unavailable before dispatch; HTTP 409 is sanitized and explicitly disallows automatic replay. Invalid input remains 400, successful legacy Task/API behavior remains compatible. The host maps a single operation entry into pure presentation; application retention/outcome types stay in Module.

Final owning 267 cases, rendering 22 cases and real browser acceptance pass. The stable checkpoint's sole generated-evidence failure passed its unchanged focused rerun; its negative original is retained. Portability and source/retained secret gates pass. Documentation retains the inherited 118-log finding, so this closure does not claim repository merge readiness. Manifests and links are verified by the final sealing receipt.

Capability create/edit/delete outcome recovery, historical detail-load fallback, durable cross-circuit recovery, routing and other module refactors remain outside this child. Capabilities-03 is authorized next in its existing SB00 -> SB01 -> SB02 -> SB03 order; Overview preparation remains gated on its closure.
