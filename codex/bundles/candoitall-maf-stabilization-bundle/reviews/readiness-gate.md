# Readiness Gate

Codex must not mark the work complete unless the following checks are satisfied or explicitly documented as blocked by environment.

## Architecture gates

- [ ] Structured-output contracts are preserved across approval/background continuations.
- [ ] Machine-critical structured output is validated before success.
- [ ] Invalid output cannot be persisted as successful workflow state.
- [ ] Bounded repair/retry exists where configured.
- [ ] Function invocation middleware enforces tool policy before execution.
- [ ] Disabled built-in tools are not attached.
- [ ] Finalizer tools are exact-once where required.
- [ ] Provider capabilities are centrally resolved and enforced.
- [ ] Sessions are not process-state source of truth.
- [ ] Generic runtime contains no calculator-specific instructions.
- [ ] Observability captures validation, repair, tool policy, finalizer status, raw hash, and final outcome.

## Test gates

- [ ] Solution build attempted.
- [ ] Unit tests attempted.
- [ ] Relevant integration tests attempted.
- [ ] Process/calculator regression attempted.
- [ ] Environment limitations documented.

## Documentation gates

- [ ] Agent output contract docs updated.
- [ ] MAF runtime stabilization docs added or updated.
- [ ] Provider capability and tool-policy docs added or updated.
- [ ] New-agent checklist added.
