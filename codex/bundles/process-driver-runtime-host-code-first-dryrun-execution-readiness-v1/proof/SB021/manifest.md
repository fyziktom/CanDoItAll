# SB021 Proof Manifest

- Gate: Hardened live process-run proof.
- Source proof: `repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs`
- Test proof: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~LiveProcessRunOpenAiSmokeIntegrationTests -v minimal`
- Negative proof: settings tests reject missing/invalid model, timeout, and token budget without logging `OPENAI_API_KEY`.
- Live proof: opt-in OpenAI process-run smoke passed 8 tests with model `gpt-4.1-mini`, timeout `180`, max total tokens `10000`.
- Changed-file SHA-256: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` `EFDDD6CE7CFA7A9708D0BCEA6117BAB1A67FE0F354B3791BAFA037043F5836FE`
- Result: Passed.
