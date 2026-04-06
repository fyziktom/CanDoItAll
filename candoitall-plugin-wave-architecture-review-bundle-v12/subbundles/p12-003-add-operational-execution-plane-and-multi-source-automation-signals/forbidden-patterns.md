# Forbidden patterns

- Do not model every operational envelope as a Workbench node.
- Do not keep singular `IAutomationSignalProvider` consumption in `AutomationWorkspaceService`.
- Do not materialize nodes implicitly on message receipt.
