# Requirement Traceability

| Input | Normalized requirement / finding | Owner | Closure evidence | Status |
|---|---|---|---|---|
| N01 live incident | R01-R09, F01-F07 | All; SB06 closes | Sanitized incident capture plus direct/shared live runs and screenshots | Solved |
| N02 stop host | R11 | Preparation/closure | `analysis/host-stop.json`; port 5032 remained stopped throughout implementation | Solved |
| N03 missing node/false claim | R01, R03-R05, R07-R09; F01-F04 | SB01-SB03, SB05-SB06 | Typed outcomes/effects, completion assessment, durable receipts, asset readback, direct/shared live committed nodes | Solved |
| N04 automatic refresh | R08; F06-F07 | SB05-SB06 | Five component cases plus both live canvases updating without reload | Solved |
| N05 smaller Ollama/tool handling | R01-R06, R12-R13; F01-F05 | SB00-SB04, SB06 | MAF 1.20 characterization, actionable validation feedback, corrected optional schema, direct/shared live tool execution | Solved |
| N06 direct/shared parity | R05-R06, R12 | SB00, SB03-SB04, SB06 | Six parity cases, relay policy/normalizer regressions, live shared run `b1b2ead6-09bc-4248-b007-d4bb74cfa30c` | Solved |
| N07 architecture/filesystem audit | R01-R13; F01-F07 | Architecture docs and all SBs | Existing boundary inventory, final source review, no new project-reference direction, Release builds and architecture gate | Solved |
| N08 bundle first, then implementation | R11 | Preparation and later user authorization | Prepared bundle retained; implementation began only after the user's explicit follow-up | Solved |
| N09 screenshot/conversation evidence | R11 | Preparation/SB06 | `inputs/reported-state.png` plus direct/shared live screenshots | Solved |
| N10 MAF 1.20/workflow assessment | R12-R13 | SB00, SB02 | Coherent 1.20 package graph, compatibility cases, workflow/cancellation regressions and completion policy | Solved |
| Agent used wrong signature | R01-R02, R12; F01/F05 | SB00/SB01 | Required-field schema corrected; malformed input remains nonexecuting and receives safe field paths; 8 feedback cases | Solved |
| Run reported Succeeded | R03-R04; F02/F04 | SB02 | Seven completion and four persisted/API receipt cases | Solved |
| Prior result absent next turn | R05; F03 | SB03 | Eight authorized two-turn projection/isolation cases | Solved |
| File exists; asset absent | R07; F07 | SB05 | Six commit/readback cases and both live committed asset receipts | Solved |
| Shared route defects | R06, R12 | SB04/SB06 | Boolean-schema normalization, empty assistant tool-call content acceptance, connector/policy tests, full live shared success | Solved |
| UI node absent | R08; F06 | SB05/SB06 | Scoped notification refresh plus observed node-count transitions 3 to 4 to 5 without page reload | Solved |