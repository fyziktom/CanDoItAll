# Historical negative — HTTP request owns provider execution

- revision: `da8cfeb8aa08917350b2433c377a8d6c6abc66dc`
- command: `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~Request_lifetime_ends_before_provider_completion_and_does_not_cancel_durable_execution" -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal`
- exit: 1
- result: 0 passed, 1 failed, 0 skipped
- failure: `TimeoutException` while awaiting the HTTP response before releasing the controlled provider

The old application service performed provider invocation inline. The request therefore could not
receive durable admission independently from paid execution, which is the exact F-010 regression.

Setup deviations: a first no-restore attempt had no assets and produced no executable proof; private
package restore was unavailable; a nested local-sibling worktree exceeded Windows MAX_PATH. The same
revision and test were then built in `C:\repositories\CanDoItAll-sb04neg`. Both temporary worktrees and
their exact junctions were removed after the expected failure.
