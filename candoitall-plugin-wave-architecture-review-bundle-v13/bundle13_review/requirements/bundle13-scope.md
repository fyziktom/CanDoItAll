# Bundle13 scope

Bundle13 is a runtime-hardening bundle, not a Workbench recovery bundle.

It must:

1. make automation runtime options configurable in production,
2. make publish / ingress / outbox idempotency atomic,
3. make due-work acquisition database-first and lease/claim based,
4. make hosted workers resilient per iteration,
5. retire or durably bridge the legacy background-job queue seam.
