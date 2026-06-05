# Semantic Invariants

- Existing governed step outcome semantics must not change.
- Missing required tools and critical tool failures must keep the same precedence.
- Declared blocked/completed/refused/waiting outcomes must map to the same step statuses.
- Branch outcome selection and recovery of explicit disposition branches must remain stable.
- Session tool observations must preserve function call/result pairing, successful result filtering, and normalized tool names.
- Browser output observations must preserve session output, execution log output, result-summary output, declared path matching, and working-directory safety.
- No-progress retry compression must not be weakened.
- All new helpers remain internal/module-local.
- Driver-readiness remains documentation-only.
