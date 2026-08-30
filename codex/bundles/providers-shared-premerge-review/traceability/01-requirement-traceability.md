# Requirement and Input Traceability

| Input / requirement | Finding / evidence | Owning units | Planned proof | Closure |
| --- | --- | --- | --- | --- |
| N01 / R01,R09 | Prioritized review; source/base hashes; old locked closure | SB01, SB09 | Public SDK behavior, frozen current-source gates, manual handoff | Not solved: repairs/proof pending |
| N02 / R01,R02,R05,R06 | SP-01..04; four performance findings; DC02 | SB01, SB02, SB04, SB05, SB06 | Positive/negative protocol/network/retention tests, allocation/query measurements, schema conformance | Not solved |
| N03 / R03,R04 | H01/H02, retention and canonical source inspection | SB03, SB04 | Synthetic decrypted capture, timeout/cancellation separation, referenced/orphan lifecycle | Not solved |
| N04 / R05,R10 | Two-pass performance report, 160-file scan, architecture maps and snapshots | SB01–SB06, SB09 | Isolated seams, no new partial, reference graph, targeted measurements | Review solved; execution proof pending |
| N05 / R06,R07,R08,R09 | DC01–04; exact package/schema/migration inventory | SB06, SB07, SB08, SB09 | Docs validator; schema/skill/live parity; EF/upgrade; historical handoff | Bundle prepared; improvements not solved |
| N06 / R10 | Supplied engineering instructions and architecture decisions | Every unit | Typed small changes, explicit failure, safe logs, no XML/partial/extra project growth | Preparation respected; verify execution |

| Finding group | Primary owner | Reopen dependency |
| --- | --- | --- |
| SP-01/SP-03/SP-04 | SB01 | SB05/SB06/SB07/SB08/SB09 |
| SP-02 | SB02 | SB05/SB06/SB07/SB08/SB09 |
| H01/H02 | SB03 | SB07/SB09; SB06/SB08 if contract changes |
| Performance orphan input | SB04 | SB05/SB07/SB08/SB09 |
| Performance cache/sets/copies | SB05 | SB06 if wire drift; SB07/SB09 |
| DC02 | SB06 | SB07/SB08/SB09 |
| DC01 | SB07 | SB08/SB09 |
| DC03 | SB08 | SB09 |
| DC04 | SB07/SB09 | Original shared-providers gates retain historical facts |

Review/preparation completion must not mark runtime repair rows Solved. At execution closure use Solved, Partially solved or Not solved with exact proof paths. Every finding is owned; conditional risks in the reports require explicit disposition, not silent deletion.
