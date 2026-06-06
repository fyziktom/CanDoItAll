# No-Core cutline

This bundle is still pre-Core. A future Process Core extraction is not safe until:

- source-family coordinators no longer depend on a broad host or dispatcher service;
- projection context no longer leaks unnecessary dispatcher nested types;
- side-effect coordinators are explicit and documented;
- architecture tests enforce dependency direction;
- build and focused projection tests pass after host shrink;
- driver-readiness remains documentation-only.

Expected next seam after this bundle: evaluate transition/finalizer context or output-context validation boundaries, not immediate Core, unless the final red-team explicitly proves readiness.
