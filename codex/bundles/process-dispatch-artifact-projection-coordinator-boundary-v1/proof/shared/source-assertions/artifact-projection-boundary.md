# Artifact Projection Boundary Source Assertions

- SHA-256 src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs: 
23974CCEBA4DBA49D9207F6C2B03E6B13B48B6E5BC54CC4BB02FC80BF21CDCCC
- SHA-256 src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs: 
828D4182B86154FD8D7DA88997F0111C4F4038A8580C027B7658AC8E04985895
- SHA-256 tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs: 
6FD6A007819793FF9BBF23551C4012ADFDC73A83AD0093383243A3BDB31B29ED
- The facade in repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs delegates source families in the required order: execution artifacts, process mock artifacts, workspace-written artifacts, existing managed artifacts, response text artifacts, provider-native browser artifacts, completed decision artifacts.
- The coordinator implementation is contained in repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs and remains a private nested boundary inside the existing dispatch service partial.
- The projection architecture assertions are in repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs.
