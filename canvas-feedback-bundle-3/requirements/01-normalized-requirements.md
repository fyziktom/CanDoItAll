# Normalized Requirements

- `R001` The selection panel must expose two launch actions for supported runtime-capable nodes: normal PowerShell and elevated PowerShell.
- `R002` Dotnet watch nodes must open PowerShell in the configured project path and run the dotnet watch command derived from the node settings.
- `R003` The same launch flow must work for other supported script and runtime nodes, including python environments and script nodes that already provide command metadata.
- `R004` Nodes without enough metadata to launch deterministically must not show the launch actions, and launch failures must return explicit actionable feedback.
- `R005` The change must preserve the existing node actions, preview behaviors, and attachment local-open flow.
- `R006` The feature must be proven with focused automated coverage and an updated execution report.
