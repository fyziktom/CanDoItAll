# Assumptions And Risks

## Assumptions

- `processes-hardening` is the correct working branch.
- The repository workflow domain model remains valuable as the canonical persistence/UI model.
- MAF should be the runtime execution engine or at least the validated execution adapter target for workflows.
- The JSON-oriented template pack remains the correct authoring format for seeded examples and user-extensible workflow templates.
- Plugin projects are intended to provide workflow executors or executor-like capabilities.

## Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| MAF 1.7.0 introduces API changes that break current integration. | Build/runtime breakage. | SB01 must isolate upgrade decision and record an API diff before editing runtime code. |
| Repository workflow graph and native MAF graph diverge. | Inconsistent preview/production behavior. | Introduce one canonical compiler/adapter and test it with golden graphs. |
| Plugin executors bypass approval or permission policies. | Unsafe external actions and data leakage. | Enforce registry-level capability/approval checks before executor invocation. |
| YAML templates compile but fail at runtime due to missing executors or invalid routes. | Broken seeded workflows. | Add strict graph/executor/route validation in SB02 and SB03. |
| Durable production policy is configured but not enforced. | False sense of reliability. | SB05 must fail production execution when durable backend is unavailable and policy requires it. |
| Live Gmail/Office365 dependencies make tests flaky. | Non-deterministic CI. | Use fake plugin connectors and mark live tests separately. |
| Long fan-out paths block shorter paths due to MAF superstep barriers. | Unexpected latency. | SB03/SB05 must document and test fan-out/fan-in topology behavior. |
| Seeding overwrites user-managed workflow definitions. | Data loss. | Preserve seed marker checks and add migration regression tests. |

## Stop conditions

Stop and report instead of forcing changes when:

- Restore/build cannot run because the environment lacks required .NET SDK or package feed access.
- MAF package upgrade produces ambiguous API breakage that cannot be resolved safely in the current subbundle.
- Runtime behavior would require destructive migration of user-managed definitions.
- Plugin execution requires secrets or external services not available for deterministic proof.
