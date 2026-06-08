# SB036 Proof Manifest

- Subbundle: SB036
- Status: Completed
- Owned requirements: REQ-007, REQ-011
- Raw notes: Prepare safe process integration handoff points without wiring runtime host
- Semantic invariant contract: bundle://proof/SB036/semantic-invariants.md
- Changed-file hash index: bundle://proof/SB045/changed-file-hashes.md
- Representative after SHA-256: EBD71A2C78FF526CD3F152BD22CD5EA3EF3F1F0A3F416CE73D7DA17CEF7682F9 for repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs and repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj
- Failing-first transcript: N/A process non-production integration-readiness closure; no standalone non-zero before transcript exists for this closure lane.
- Passing transcript: bundle://proof/SB024/transcripts/process-transcript-readonly-adapter-integration-tests.txt
- Passing transcript: bundle://proof/SB036/transcripts/closure-proof-index.txt
- Source assertion transcript: bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Anti-stub audit transcript: bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Downstream smoke proof: bundle://proof/SB045/transcripts/unit-tests-excluding-known-unrelated-stale-fixtures.txt
- Production source proof: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs and repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj
- Full unit residual transcript: bundle://proof/SB045/transcripts/full-unit-tests-no-restore.txt

## Changed File Hashes
| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| repo://CanDoItAll.slnx | 69BD7541241B040D19616BE13AEE96D9FB367697D213D0D43A9E83F71F3163D9 | ED13898D741FF61434D6C32525A055F3011B9C799882944F88C19F3F78F09653 |
| repo://src/CanDoItAll.Processes.Drivers.Abstractions/Audit/ProcessDriverRedactionPolicy.cs | not-in-HEAD | 80B178856D6E81FCD4C331C45F1F12CCAD37EA7B3E9F924EF3473C6D56F152AF |
| repo://src/CanDoItAll.Processes.Drivers.Abstractions/Evidence/ProcessDriverEvidencePolicy.cs | not-in-HEAD | 68AAB0B844706C3D66F89B86926723CB6BB2730C7DBFCE92D243B74C61BB427E |
| repo://src/CanDoItAll.Processes.Drivers.Abstractions/Permissions/ProcessDriverCapabilityScopeRules.cs | 8245317CE4A986D76F61993447F531B2E90024A88A2549C4CCBB922C7A892286 | 480292CFE752E91F79475E935E054394B5C9E04278651E7AAA43C8D34A181F19 |
| repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverContractVersion.cs | 31E26C5570C5DF5A16DC42F0BB32D1DF4B8D0DB2D434C55D1267DAC5C91B4CCC | 23759FA40A4506657A97AEA9FF9C86F6ADADBB6DE213F2038965B147AD4FCF11 |
| repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverDiagnostic.cs | FAC2C9D4ED2F5D778652F4BE7DAB1B00E597CDD30B14CA314E9D011CCA24495F | DB6F267FD51F8F6482B333770A75FB752C3ECD2318762073EA874376E8CD1D13 |
| repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs | CAC4ACD8CD187720D464FF178AB7007F9DDF4B06FDF0999EDEB9EB90FD59E27D | 588388F6562BDE97A1104E68235D199AC52215700D7ED7E5EA645F8CB1B3CB0F |
| repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptDiagnosticParsers.cs | not-in-HEAD | 66AA537871674EBF3117C95BF9B2A0AACF839C6AE523B8B7A1466AA1E21BD15B |
