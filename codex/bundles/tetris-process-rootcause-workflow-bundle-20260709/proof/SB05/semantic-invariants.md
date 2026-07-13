# SB05 Semantic Invariants

- Invariant ID: `SB05-INV-provider-boundary`
- Source raw note: GPTPro RC5 and the user request to remove domain leaks from generic process runtime and dispatcher layers.
- Expected behavior: Generic recovery instructions describe diagnostics, while Workbench advice adds .NET/software-delivery repair details through an injected provider.
- Disallowed shallow implementation: Moving the same domain text to another generic helper or keeping default recovery tied to tool names.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `Request_rework_appends_diagnostic_specific_packet_from_runtime_receipt` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Workbench/Processes/DotNetSoftwareDeliveryRecoveryAdviceProvider.cs`
- Production assertions: `WorkbenchModuleServiceCollectionExtensions` registers the Workbench advice provider and generic process DI registers only generic advice.
- Red-team negative case: Generic builder tests assert the absence of software-delivery branch names and .NET runtime tool literals.
- Downstream dependency check: Dispatcher and operator tests inject the Workbench advice provider only where domain-specific advice is expected.
