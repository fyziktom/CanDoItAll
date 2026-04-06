# Acceptance

This subbundle closes only when:
- the explicit repair seam exists,
- stale system-managed rows and orphan layouts can be retired there,
- the read seam no longer performs this repair,
- the repair is proven idempotent by tests.

Target acceptance:
Cleanup still works, but only when intentionally invoked through the dedicated repair boundary.
