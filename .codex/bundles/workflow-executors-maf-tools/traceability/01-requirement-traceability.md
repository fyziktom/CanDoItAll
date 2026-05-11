# Requirement Traceability

| Requirement | Subbundle | Planned Proof |
|---|---|---|
| R01 | 01, 05 | Unit tests for catalog/descriptors; UI setup renderer metadata shown in inspector/toolbox. |
| R02 | 01, 04, 06 | Validator tests for bad timeout/retry; runtime scenario for timeout/failure. |
| R03 | 01, 04 | Serialization/validation tests proving legacy nodes still load and executor nodes invoke by id. |
| R04 | 02 | Build scan for ClosedXML references; wrapper tests. |
| R05 | 02, 06 | Scenario reads workbook, writes workbook, and emits Markdown summary. |
| R06 | 03, 06 | Storage scenarios for list/stat/read/write/append/search/diff. |
| R07 | 03, 06 | Project structure read/subtree and asset creation scenario or explicit host blocker. |
| R08 | 03, 06 | HTTP JSON/text success and invalid scheme/size-limit failure scenarios. |
| R09 | 03, 06 | Image provider attempt with artifact proof or explicit provider blocker. |
| R10 | 01, 07 | Catalog/follow-up list contains planned generic executor entries. |
| R11 | 04, 06 | MAF workflow scenario proves executor invoker is called. |
| R12 | 04, 06 | Run event/artifact inspection for artifact-producing executor. |
| R13 | 05 | Browser screenshot and DOM/action proof for second-level right-click menu. |
| R14 | 05 | Browser screenshot and DOM/action proof for workflow executor toolbox. |
| R15 | 01, 05 | Inspector/settings tests and screenshot proving descriptor-backed setup. |
| R16 | 06 | Execution report contains 20 real-world scenarios with result/proof. |
| R17 | 06 | Execution report contains `gpt-5-mini` and `gptoss20b64k` attempts. |
| R18 | 08 | Browser screenshot with workflow toolbox floating window. |
| R19 | 08 | Browser screenshot with workflow selection floating window. |
| R20 | 08 | Browser proof that toolbox and right-click create open modal/composer before adding. |
| R21 | 08 | Browser proof that node double-click opens details/edit modal. |
| R22 | 08 | Browser proof for workflows page tabs and preserved content. |
| R23 | 09 | HTTP/API observer smoke for catalog, run start/cancel, events, artifacts, pending requests, analytics. |
| R24 | 10 | PostgreSQL database creation and testing-instance startup proof. |
| R25 | 10 | Seed counts and 20-example scenario matrix with project structures. |
| R26 | 10, 11 | Scenario failures mapped to repairs, retests, blockers, or follow-up subbundles. |
