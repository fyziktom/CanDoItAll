# Follow-Up Proof Index

All follow-up Behavioral evidence required by SB07, SB08, SB09, and the final gate is captured and reviewed. No pending closure slot remains.

## Verified Evidence

| Proof id | Owner | State | Evidence |
| --- | --- | --- | --- |
| `P07-SOURCE` | SB07 | `Verified` | Typed default-off `PagedRecordResultsScrollMode`, controlled Directory/Workforce dialogs, generation invalidation, and contextual workbench titles in the source files cited by `bundle://subbundles/07-directory-workforce-catalogs-and-dialogs/README.md`. |
| `P07-COMPONENT` | SB07 | `Verified` | Exact focused selection exited `0` with `37 passed`, `0 failed`, `0 skipped` in `1m50s`; see `bundle://proof/final-validation.md`. |
| `P07-BROWSER-NORMAL` | SB07 | `Verified and inspected` | Populated Directory and Workforce catalogues at `1800x1100`, measured full-width composition, real bounded overflow, and successful second-page navigation; see `bundle://proof/SB07/browser-normal-and-dialog-review.md`. |
| `P07-BROWSER-DIALOG` | SB07 | `Verified and inspected` | Amina and Lucas record dialogs, tab/content scrolling, manager/allocation data, visible action regions, artifact lengths, and SHA-256 digests; see the SB07 browser review. |
| `P08-HTTP` | SB08 | `Verified` | Real-host `CrmHrApiIntegrationTests` exited `0` with `2 passed`, `0 failed`, `0 skipped` in `30s`; positive and invalid-reference/query negatives are named by the SB08 completion record. |
| `P08-SKILL` | SB08 | `Verified` | Repo and active-root CRM-HR skills each returned `Skill is valid!`; all three corresponding file hashes matched. |
| `P09-SEED-FIRST` | SB09 | `Verified` | Public-API-only deterministic scenario with `DEMO-CRMHR-*` identities and heterogeneous workforce/recruiting state; see `bundle://proof/SB09/seed-first-run.md`. |
| `P09-SEED-REPEAT` | SB09 | `Verified` | Immediate reconciliation performed zero creates, writes, replacements, or conversions and reused all tracked business identities; see `bundle://proof/SB09/seed-repeat-run.md`. |
| `P09-READBACK` | SB09 | `Verified` | Bounded public reads returned `78` parties, `32` workforce records, `8` applications across seven stages, and the expected linked child data; see `bundle://proof/SB09/api-readback.md`. |
| `P09-BROWSER` | SB09 | `Verified and inspected` | Populated Directory, Workforce, and Recruiting at `1800x1100`; Omar's linked hiring/workforce context; repaired render-race negative; final console `0` errors and `0` warnings; see `bundle://proof/SB09/browser-review.md`. |
| `P09-HOST-5032` | SB09 | `Verified` | Final Release root/access returned HTTP `200`, CRM-HR totals were `78/32/8`, stderr was empty, and no server error pattern appeared; see `bundle://proof/SB09/host-5032.md`. |
| `PF-BUILD-TEST` | Final | `Verified` | Release build `0` errors; feature UI `37/37`; race regression `1/1`; focused API `2/2`; broader CRM-HR integration `35/35`; see `bundle://proof/final-validation.md`. |
| `PF-STATIC-ARCH` | Final | `Verified` | No project reference changed; AppComponents remains domain-neutral; Web transport delegates canonical services with no direct persistence; no critical performance anti-pattern was found. |
| `PF-COMPLETED-VALIDATOR` | Final | `Verified` | Canonical initiative-profile prepared and completed stages pass; exact commands/results are in `bundle://proof/validator-results.md`. |

## Behavioral Adequacy Summary

- Semantic positive: API-created records agree across bounded reads and the actual Directory, Workforce, Recruiting, dialog, interview/task/support, and conversion views.
- Adversarial negative: repeated seeding reuses stable identities with zero mutations; structured invalid-reference/query failures remain covered; the populated Recruiting render race was reproduced, fixed, and covered by a dedicated regression.
- Shallow-pass traps rejected: page 2 was reached in both catalogues, result regions had actual bounded overflow, screenshots used the persisted Release runtime, and Web delegates canonical services rather than acknowledging commands without work.
- Anti-stub audit: no direct SQL/EF seed, startup hook, fixture-only UI branch, hidden fallback list, fake financial state, `TODO`, or `NotImplemented` path supplied the proof.

## Explicit Residual Risks

- The Release build still reports the existing high-severity `NU1903` advisory for `System.Security.Cryptography.Xml` `10.0.7`.
- The broader all-unit repository baseline remains non-green for unrelated existing workflow snapshot, seed-version/hygiene, stale in-memory fixture, and secret-scan failures. It is not claimed as closure evidence.
- CodeAnalytics and Components transports were unavailable; direct source/dependency review, focused tests, Release build, browser proof, and host proof provide the recorded gate evidence.
