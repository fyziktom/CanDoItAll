# Pre-SB02 cancellation regression proof

The disposable detached worktree used implementation parent `c90f56497`. A test was added only in that
worktree and asserted that a durably `CancellationRequested` operation cannot complete as Succeeded.

## Environment diagnostics

- The nested worktree initially resolved sibling source roots under `.worktrees`; the rerun supplied the
  actual `CanDoItAll.Components` and `CanDoItAll.FileTools` roots.
- Windows path length then blocked an unrelated template copy; the final historical command set
  `CopyRepositoryTemplatesToOutput=false`.
- The disposable worktree was removed after proof capture.

## Final historical command

```text
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore -nologo -v:minimal -p:CanDoItAllComponentsRepositoryRoot=C:\repositories\CanDoItAll.Components -p:CanDoItAllFileToolsRepositoryRoot=C:\repositories\CanDoItAll.FileTools -p:CopyRepositoryTemplatesToOutput=false --filter "FullyQualifiedName~LlmChatCancellationRedGateTests"
```

Exit: `1`. Result: **0 passed, 1 failed, 0 skipped**.

Failure:

```text
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.Reflection.TargetInvocationException)
```

This directly proves the old transition accepted committed cancellation and could produce Succeeded.
The equivalent current-source regression passes 1/1 at
`be36fedb2ce329af6021cd2330eb6162d8ef2db4`.
