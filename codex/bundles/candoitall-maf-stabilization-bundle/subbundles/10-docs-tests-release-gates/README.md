# 10 - Documentation, Regression Tests, and Release Gates

## Objective

Close the stabilization work with documentation, test coverage, and release gates that prevent future regression to prompt-only or markdown-parsed agent decisions.

## Primary files to inspect


- `docs/agent-output-contracts.md`
- Add or update `docs/maf-runtime-stabilization.md`
- Existing Codex bundle reports
- Test project files
- CI/build scripts if present


## Required implementation tasks


1. Update documentation to explain:
   - structured output contracts
   - validator registry
   - repair/retry
   - finalizer tools
   - tool policy middleware
   - approval continuation contract preservation
   - provider capability matrix
   - session/context policy
   - MAF workflow alignment
   - observability schema
2. Add a regression checklist for new agents/process steps.
3. Add tests or static checks that detect unsafe patterns:
   - `structuredOutput: null` in critical continuation paths
   - prompt-only JSON for machine-critical output
   - markdown-derived workflow decisions
   - disabled built-in tools attached
   - generic runtime domain-specific hints
4. Add CI/release gate commands if the repository supports them.
5. Create a final execution report summarizing each subbundle.


## Required tests


Documentation checks:
- Docs match implemented code and installed MAF API names.
- New developer can add a typed agent output safely using the docs.

Regression tests/static checks:
- Critical path structured-output preservation.
- Tool policy enforcement.
- Finalizer exact-once behavior.
- No calculator-specific text in generic runtime.
- No markdown-decision parsing in process continuation code.

Release checks:
- Build passes.
- Unit tests pass.
- Focused process/agent integration tests pass.
- Environment-limited tests are documented.


## Risks and constraints


- Documentation must not drift from implementation. Prefer examples copied from actual tested code.

