# CDA-UI-SEAMS-AGENT-DIAGNOSTICS-01

Status: implementation in progress. Owner authorization: latest attachment and explicit chat authorization, 2026-09-08.

Governance is an accepted predecessor. This single bundle owns Diagnostics reads, session, allowlisted rendering, extraction and the existing sandbox specimen. Routes, runtime policy and sibling source are unchanged.

The Module owns three independent read lanes and effects. The UI assembly owns immutable presentation, explicit lane state and typed refresh/retry intents. Each accepted lane retains its own request identity; failed refreshes retain only explicitly stale data. Cancellation suppresses publication while its request retains token resources until completion. Dashboard failures describe the global snapshot; recent failures describe only the latest twelve runs. Runtime payloads are omitted; known local boundary policy is described explicitly; timestamps are invariant UTC.

Implementation sequence: preserve public behavior through focused lifecycle and presentation tests; replace the integrated renderer; validate the extracted graph and real Web/Parity/Fast browsers; then continue into the separately authorized History seam. Final broad stable and static gates run once after both surfaces freeze. No intentionally failing checkpoint is deliverable.

Evidence uses one report, one aggregate validation summary and one manifest. Raw logs stay ignored. Combined Diagnostics/History budget: ten files, four MiB, two screenshots. The existing five-file Components delivery patch remains an external publication dependency and is preserved byte-for-byte.
