# SB03 Anti-Stub Audit Transcript

- Invariant ID: `WEB-SB03-001`

Command:

```powershell
rg -n "ApplyCreatedSurfaceNodeAsync|AddSurfaceLink|Quick_sibling_note_insertion_persists_downward_stack_shift|WrapDbContextFactoryWithCreateCounter|Assert\.Equal\(2, createCounter\.CreateCount\)" src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor tests\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs -S
```

ExitCode: 0

Output:

```text
src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor:1029:    private async Task ApplyCreatedSurfaceNodeAsync(
src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor:1054:        AddSurfaceLink(
src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor:1090:    private static void AddSurfaceLink(
src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor:1639:        await ApplyCreatedSurfaceNodeAsync(created, parentNodeId, prepared.PendingLinks, placementPlan.FollowUpMoves);
tests\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:71:    public async Task Quick_sibling_note_insertion_persists_downward_stack_shift()
tests\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:130:        Assert.Equal(2, createCounter.CreateCount);
tests\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:1154:    private static void WrapDbContextFactoryWithCreateCounter(IServiceCollection services)
```

Audit conclusion: no stub-only proof; production surface-patch code is present and the test measures the persistence path instead of only checking markup.
