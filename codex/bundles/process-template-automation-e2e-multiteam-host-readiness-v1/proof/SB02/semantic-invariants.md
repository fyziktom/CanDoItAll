# SB02 Semantic Invariants

- Invariant ID: `SB02_INV_002`
- Source raw note: Representative templates must include multi-team development and the mapping must be source-backed.
- Expected behavior: Callers can resolve `software-delivery` back to both software-development and multi-team-development representative families.
- Disallowed shallow implementation: A flat catalog row can claim multi-team support while no caller can prove which template backs that family.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt` shows the baseline lacked reverse family lookup.
- Passing test: `bundle://proof/SB02/transcripts/focused-test.txt` shows the reverse mapping and existing source-backed template test passed.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateCatalogInventory.cs` before SHA-256 `C0428A525CB1261505582836B54E88C08EDF403FE242B8BA77195CF8ACDD0285`, after SHA-256 `4BE61710CFFFBB6EF41748374D7CFA39E3B956902B8FC190010972F4C299BFD1`; `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs` before SHA-256 `6EA9155F8C43AB5DECBB4C9ADBF3A11860368A06031B11C586CE2B2B330B70AC`, after SHA-256 `2CCC0F8524D5BBDB485801F4F71116EFA5732347960F1DE030E4565698F49489`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt` shows the new lookup and exact expected family/resolution pair.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt` rejects the previous catalog shape.
- Downstream dependency check: SB03-SB05 may proceed because representative software, Blazor/.NET, business, and multi-team catalog identities are queryable and source-backed.
