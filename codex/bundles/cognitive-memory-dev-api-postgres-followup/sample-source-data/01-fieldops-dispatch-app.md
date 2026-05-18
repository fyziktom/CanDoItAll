# FieldOps Dispatch App

FieldOps Dispatch is an offline-capable work management application for regional utility contractors. The core problem is not task CRUD; it is preserving operational truth when crews, dispatchers, ERP inventory, and GIS asset data all change while connectivity is unreliable.

The architecture uses a local client command queue and a server-side conflict ledger. Mobile devices store work-order transitions, checklist decisions, photos, part reservations, and signatures as append-only events. The server accepts events, validates them against ERP and GIS state, and creates explicit conflict records when a crew action and dispatcher action cannot both be accepted.

The dispatcher console is exception-driven. It should prioritize late crews, access blockers, missing evidence, ERP reservation failures, and conflict records. The mobile app is workflow-driven and must remain usable for a full disconnected shift.

Important risks include safety attestation bypasses, duplicate media uploads during retry, clock skew, and inventory reservations revoked after a crew starts work. The pilot gate requires deterministic sync replay, visible conflict handling, and exportable safety evidence packets.
