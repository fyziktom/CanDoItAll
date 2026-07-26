# Resolve simple application contract

Choose exactly one `applicationKind`: `UI`, `WebApi`, `Console`, or `Library`. Record the technology stack, approved product root, entry project or command, acceptance criteria, exclusions, and the proof required for that kind.

Use `SimpleSupported` for bounded local work. Use `SpecialistReviewRequired` only when the work includes an explicit unsupported trigger: external or production deployment, privileged operating-system action, authentication or authorization boundary, secrets, personal or regulated data, payments, destructive migration, production infrastructure, or third-party publishing. Name the trigger. Do not add generic security or deployment gates to supported work.

UI proof may require runtime startup, representative browser interaction, screenshot, browser state, console diagnostics, and cleanup. Web API proof may require runtime startup, HTTP assertions, logs, and cleanup. Console proof requires an invocation, arguments, stdout/stderr, exit code, and observable result. Library proof requires build, tests, and public-contract or consumer evidence; it does not require runtime or browser proof.

Select exactly one branch: `simple-supported` or `specialist-review-required`.

## Evidence

Write the managed contract artifact and cite project-structure identifiers used to resolve it.
