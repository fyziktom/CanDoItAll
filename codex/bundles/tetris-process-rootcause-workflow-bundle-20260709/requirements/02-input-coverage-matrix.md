# Input Coverage Matrix

| Raw input | Normalized requirements | Owning subbundles | Planned proof |
|---|---|---|---|
| GPTPro RC1 branch-unaware receipts | R01, R02 | SB02, SB03 | Parser tests, branch enforcement tests, semantic proof manifest |
| GPTPro RC2 completion failures cannot route branch | R03, R04 | SB04 | Incident regression, route metadata tests, runtime gate findings source assertion |
| GPTPro RC3 duplicated receipt contract | R05 | SB03, SB07 | Dedup tests, template migration audit |
| GPTPro RC4 retry policy treats branch-routable defect as retry | R03, R11 | SB00, SB04 | Retry-budget regression test |
| GPTPro RC5 recovery builder domain leakage | R06 | SB05 | Forbidden-token architecture test, provider tests |
| GPTPro RC6 QA template ambiguity | R02, R07 | SB07 | Template diff, prompt behavior tests |
| GPTPro RC7 missing acceptance criteria matrix | R08 | SB08 | Calculator/Tetris-like criteria fixtures, matrix mapping tests |
| GPTPro RC8 test gaps | R11 | SB00, SB11 | Failing-first and passing unit/integration transcripts |
| GPTPro RC9 MAF receipts captured but adapter mapping too binary | R02, R03, R10 | SB03, SB04, SB10 | Current-run receipt tests, trace assertions |
| User warning about other templates/artifacts | R07, R08 | SB06, SB07, SB08 | Template inventory and migration/exemption table |
| C# architecture quality request | R06, R11 | SB01, SB05, SB11 | Architecture gate, CodeAnalytics evidence, dependency check |
| Corrective request: remove partial adapter architecture and split responsibilities | R12 | SB12 | Zero-partial source assertion, thin-adapter assertion, direct collaborator tests, composition smoke |
| Corrective request: keep generic runtime/dispatcher domain-neutral | R13 | SB13 | Forbidden-token scan plus positive/negative driver-policy tests |
| Corrective request: update OpenAI package only when compatible and observe autonomous Tetris E2E | R14 | SB14 | Package compatibility transcript or no-update decision; production dispatch/process/agent/tool/provider evidence |
