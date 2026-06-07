# QA Prompt

Validate this bundle as a runtime/service-only Process Core expansion.

- Confirm each subbundle row is updated individually.
- Confirm no UI, browser, mobile, media, Razor, CSS, JS, TS, or screenshot artifacts were introduced.
- Confirm Core references only approved dependencies and approved namespaces.
- Confirm production driver work remains documentation/test-only.
- Confirm behavior-changing critical subbundles have failing-first, passing, source assertion, anti-stub, changed-file hash, and semantic invariant proof under `proof/SBxx/`.
- Confirm final raw-note closure cites proof artifacts rather than prose-only claims.
