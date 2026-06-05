# Proof Strategy

Every production movement must be backed by both source scans and focused tests.

Required proof families:

- no Process Core / no driver production API scan,
- no MAF/Tooling/product dependency regression scan,
- no UI diff and no prohibited viewport proof path scan,
- header selection parity tests,
- candidate hydration parity tests,
- subprocess/workflow/direct-agent route candidate tests,
- technical-agent binding/access mutation tests,
- failed-run/recovery execution selection tests,
- full solution build,
- bundle prepared/completed validator.

Browser proof should be `N/A` for all subbundles unless UI is unexpectedly touched. If it is touched, only large desktop/PC proof is allowed.
