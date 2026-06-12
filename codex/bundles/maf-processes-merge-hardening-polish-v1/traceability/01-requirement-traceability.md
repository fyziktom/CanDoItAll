# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Remove tracked bundle artifacts | `requirements/01-normalized-requirements.md#R01` | `subbundles/01-repository-artifact-hygiene-and-bundle-leak-cleanup` | `git ls-files` forbidden-path scan; unit guard | Preserve `codex/skills/bundles/**`. |
| Tighten ignore rules | `requirements/01-normalized-requirements.md#R02` | `subbundles/01-repository-artifact-hygiene-and-bundle-leak-cleanup` | `.gitignore` diff + tracked path scan | Avoid over-ignoring source tooling. |
| Remove SB/bundle names in tests | `requirements/01-normalized-requirements.md#R03` | `subbundles/02-test-naming-neutralization-and-guardrails` | source scan + updated test names | Observed in `ProcessDriverVerificationGatewayTests.cs`. |
| Add future guardrails | `requirements/01-normalized-requirements.md#R04` | `subbundles/02-test-naming-neutralization-and-guardrails` | new/updated unit tests | Prefer tracked-file scanner. |
| Preserve MAF decoupling | `requirements/01-normalized-requirements.md#R05` | `subbundles/04-driver-boundary-and-gateway-hardening` | existing + expanded MAF boundary tests | Do not reintroduce MAF -> Processes. |
| Keep Process Core clean | `requirements/01-normalized-requirements.md#R06` | `subbundles/04-driver-boundary-and-gateway-hardening` | project/source reference scans | Core should reference Contracts only. |
| Move software-delivery domain logic | `requirements/01-normalized-requirements.md#R07` | `subbundles/03-software-delivery-domain-proof-driver-extraction` | source scan + focused tests | Avoid broad runtime rewrite. |
| Keep drivers verification-only | `requirements/01-normalized-requirements.md#R08` | `subbundles/04-driver-boundary-and-gateway-hardening` | source scans + driver tests | No runtime host/registry/DI/discovery. |
| Keep gateway explicit | `requirements/01-normalized-requirements.md#R09` | `subbundles/04-driver-boundary-and-gateway-hardening` | gateway tests | No generic `Verify(lane, object)`. |
| Preserve working process behavior | `requirements/01-normalized-requirements.md#R10` | `subbundles/05-merge-validation-and-live-process-closure` | process tests + smoke evidence | Include live run if environment is available. |
| Keep merge-safe scope | `requirements/01-normalized-requirements.md#R11` | all subbundles | git diff review | No broad dispatcher-runtime isolation. |
