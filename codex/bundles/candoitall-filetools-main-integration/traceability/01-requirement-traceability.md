# Requirement Traceability

| Requirements | Owning subbundles | Planned proof | Closure path |
| --- | --- | --- | --- |
| R001 | preparation, SB18 | git diff/source status | raw note N001 |
| R002-R004 | SB02, SB05 | native contract/registry tests, dependency/source audit | N002-N003 |
| R005 | SB03, SB05 | governed filesystem semantic/security proof | N003 |
| R006 | SB04, SB05 | fake transport/live opt-in provider positives and unsupported negatives | N003 |
| R007 | SB02, SB08 | JSON compatibility/settings/cache validation | N002-N004 |
| R008 | SB01, SB06, SB18 | FileTools build/pack/validate, SHA-256, main restore/assets | N005 |
| R009 | SB06, SB09, SB17, SB18 | before/after `.csproj`, CodeAnalytics dependencies/cycles | N003-N010 |
| R010-R012 | SB07, SB09, SB10, SB16 | governed authorization/handle/endpoint/content/save proof | N006-N007 |
| R013-R014 | SB08, SB09, SB18 | governed Disabled/isolation/revision/distributed negative proof | N008 |
| R015-R016 | SB10, SB11 | project pilot service/component/Playwright proof and cleanup gate | N009-N010 |
| R017-R018 | SB12 | filter/source fingerprint tests and desktop card/dialog flow | N010 |
| R019 | SB13, SB17 | no-new-partial source proof, node auth, floating-window browser proof | N010-N011 |
| R020 | SB14, SB17 | Processes-owned policy, live mutation, dialog/browser proof | N010-N011 |
| R021 | SB15, SB17 | governed source/promotion persistence and red-team proof | N010-N011 |
| R022-R023 | SB16, SB17, SB18 | governed renderer/save/migration/anti-duplicate proof | N007,N010-N011 |
| R024-R025 | SB01, all UI SBs, SB18 | Components MCP records, desktop-only browser analytics | N012 |
| R026-R027 | SB05, SB09, SB11, SB17 | architecture gates, old-owner shrink/no-partial proof | N003,N013 |
| R028 | SB02-SB18 as applicable | error/log masking negative tests | N006,N014 |
| R029-R030 | all SBs, SB18 | tiered proof, validators, raw note closure | all notes |
| R031-R033 | SB02-SB05, SB08, SB10, SB18 | typed budgets/capabilities, 100,000-entry structural counters, search cancellation/retention proof | N015 |
| R034-R036 | SB03-SB05, SB07-SB10, SB18 | pooled streaming transport, bounded leases, scoped anti-pattern scan, masked metrics | N015 |
| R037-R039 | SB07, SB10, SB13, SB16-SB18 | typed intent tests, zero-browser-call spies, image/PDF double-click characterization and browser proof | N016-N017 |
| R040 | SB05, SB11, SB17, SB18 | measured baselines, architecture/performance reviews, final regression envelope | N015-N017 |

## Source-To-Architecture Map

- Legacy Storage/integration/cache decisions -> `architecture/05-storage-filetools-contract-map.md`, `06-authorization-handles-and-effects.md`, `07-cache-and-revision.md`.
- FileTools host security/docs -> SB06-SB08, SB10, SB16.
- Current storage/endpoint sources -> SB02-SB08.
- Current module/UI hotspots -> SB10-SB17.
- Performance skills/current hot paths -> `analysis/03-dotnet-performance-audit.md`, `architecture/10-performance-and-scale.md`, SB02-SB05/SB10/SB13/SB16/SB18.

## Closure Rule

Execution updates this table only when ownership or proof changes materially. Final raw-note status belongs in `reviews/01-execution-report.md`; a row cannot close Solved from intent, file existence, test count, or screenshot alone.

## Final Closure

- R001-R040: `Pass` on 2026-07-13 through their owning subbundle proof plus the governed SB18 package, architecture, security, scale, browser, raw-note, and validator evidence indexed by `proof/SB18/manifest.md`.
