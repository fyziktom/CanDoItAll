# P13-005 — Retire the legacy background-job queue seam

The repo still exposes the old queue-based background job path while the new automation runtime plane exists. That is confusing and dangerous before plugin adoption.
