# Data Sources UI tests

Backend-free state, renderer and dependency-boundary tests for the actual Data Sources
components and scenario owner. The suite covers acquired drafts/raw input, password
intent capture, exact target lifetimes, command admission, closed-view results, uncertain
outcomes, explicit transfer groups and schema-read refusals. Separate Integration and
Playwright tests exercise real profile encryption, PostgreSQL owners and restart activation.

Run with `dotnet test tests/Components/CanDoItAll.Workspace.DataSources.UI.Tests`.
