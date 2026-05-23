# Failing-First Transcript Exemption

Command: dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessRunAutomationDispatchServiceTests --no-restore

ExitCode: 1

This subbundle is a runtime hardening change driven by a live production-like process failure already captured in `bundle://evidence/live-run-9228abba-snapshot.json`.

The first local validation command failed before tests executed because `CanDoItAll.Web` PID `46392` had locked assemblies under the web project's build output. That failure was environmental and non-production-code-related. After stopping the running demo app, the same focused test command passed.

No intentional failing unit test was added because the live run evidence is the failing-first input and the implementation proof is covered by the focused passing test transcript.
