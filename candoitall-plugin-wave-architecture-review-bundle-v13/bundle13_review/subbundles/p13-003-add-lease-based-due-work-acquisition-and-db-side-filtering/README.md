# P13-003 — Add lease-based due-work acquisition and DB-side filtering

The current dispatcher/outbox processors still load candidate work into memory and do not use an atomic lease/claim boundary. This is not execution-grade for runtime scaling.
