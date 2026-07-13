# Input Coverage

| Note | Literal scope | Requirement IDs | Owners | Planned closure |
| --- | --- | --- | --- | --- |
| N001 | prepare bundle only | R001,R030 | preparation, SB18 | bundle plus requested `.gitignore` diff and final no-product-implementation audit |
| N002 | Storage Driver first | R002-R007 | SB02-SB05 | storage gate passes before UI |
| N003 | proper architecture with new C# skills | R003-R009,R026-R027 | SB02-SB09, SB17 | C# artifacts/checkpoints/review gates |
| N004 | proper testing before UI | R005-R014,R029 | SB02-SB09 | affected tests, host smoke, semantic negatives |
| N005 | integrate standalone FileTools packages | R008-R009 | SB01,SB06 | validated packages/hash/reference/asset proof |
| N006 | secure host effects | R010-R011,R028 | SB07,SB09,SB18 | handle/endpoint red-team proof |
| N007 | show/edit known files with FileInteraction | R012,R022-R023 | SB10,SB16 | read-only pilot then governed edit migration |
| N008 | cache some listings, never hide live changes | R007,R013-R014 | SB08 | policy matrix/Disabled/revision proof |
| N009 | test one simple UI case first, like project-file search | R015-R016 | SB10-SB11 | pilot and cleanup progression gate |
| N010 | continue with more complex user stories | R017-R023 | SB12-SB16 | per-story behavior/browser gates |
| N011 | cover legacy named main-module surfaces | R017-R023 | SB12-SB16 | Projects, Workbench, Processes, Resources, interaction |
| N012 | large desktop only; no small/medium work | R024-R025 | all UI SBs | 1900x1200 + 1440x900 only |
| N013 | force architecture review/refactor/cleanup after phases | R026-R027 | SB05,SB09,SB11,SB17 | hard progression gates |
| N014 | maintainability/security/readability/explicit errors/logs | R003-R030 | all applicable SBs | architecture, negative, log-masking, anti-stub proof |
| N015 | many files require .NET anti-pattern and scale-safe design | R031-R036,R040 | SB02-SB05,SB08,SB10-SB11,SB17-SB18 | scoped performance audit, structural large-source/fake-transport counters, measured regression gates |
| N016 | preserve Project Structure image/PDF node double-click dialog | R039-R040 | SB13,SB16-SB18 | characterization, component/Playwright behavior and lifecycle proof |
| N017 | known file uses direct FileInteraction; browser only for collections | R037-R040 | SB07,SB10,SB13,SB16-SB18 | typed intent/direct adapter tests and zero-browser-call spies |
| N018 | track bundles; preparation only | R001,R030 | preparation,SB18 | `.gitignore` no longer excludes bundles; no product implementation diff |

No note is marked Solved during preparation. SB18 assigns `Solved`, `Partially solved`, or `Not solved` from execution evidence.
