using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static bool ContainsCalculatorContext(DispatchCandidate candidate)
    {
        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary);

        return contextText.Contains("Calculator", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCalculatorRecoveryChecklist(string missingConcreteImplementationProofSummary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Calculator recovery checklist for this retry:");
        if (!string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
        {
            builder.AppendLine($"- Last concrete proof failure: {missingConcreteImplementationProofSummary}.");
        }

        builder.AppendLine("- Do not call `workspace_dotnet_new` again if either `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj` or `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj` exists.");
        builder.AppendLine("- If `external-target/C/programovani/csharp/calculator/Calculator.csproj`, `external-target/C/programovani/csharp/calculator/Program.cs`, or `external-target/C/programovani/csharp/calculator/Components` exists at the output-root level, the host was scaffolded in the wrong place. Do not build that root host or create a second project under it in the same attempt; return Blocked/Failed so the next clean run can start from the correct outer-root shape.");
        builder.AppendLine("- If `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj` is a directory, do not write or delete it repeatedly. That path shape is corrupt; stop targeting it, report the path-shape failure, and continue only from a clean sibling test project path on a clean retry.");
        builder.AppendLine("- First read these exact files when present: `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj`, `external-target/C/programovani/csharp/calculator/Calculator/Program.cs`, `external-target/C/programovani/csharp/calculator/Calculator/CalculatorEngine.cs`, `external-target/C/programovani/csharp/calculator/Calculator/Components/Routes.razor`, `external-target/C/programovani/csharp/calculator/Calculator/Components/Pages/Home.razor`, `external-target/C/programovani/csharp/calculator/Calculator/Domain/CalculatorEngine.cs`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/UnitTest1.cs`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/CalculatorTests.cs`, and `external-target/C/programovani/csharp/calculator/Calculator.Tests/CalculatorEngineTests.cs`.");
        builder.AppendLine("- Repair, in place, with `workspace_write_file`: keep `Calculator/Calculator.csproj` as a net10 Blazor Web App project without ASP.NET Core 7 component package references; keep `Calculator/Program.cs` on the generated `WebApplication`/`AddRazorComponents`/`MapRazorComponents<App>()` hosting path; add `using Calculator.Domain;` and `builder.Services.AddScoped<CalculatorEngine>();` before `builder.Build()` when the page injects the engine.");
        builder.AppendLine("- Repair `Calculator/Components/Pages/Home.razor` as the `/` route instead of editing `Components/Routes.razor`; `Routes.razor` must remain the Router host without `@page`.");
        builder.AppendLine("- If the host build reports `RZ9988`, `@page directive must specify a route template`, or `@page \"\"` in `Home.razor`, the next mutation must set `Home.razor` to `@page \"/\"` before any test-project repair or test rerun.");
        builder.AppendLine("- Replace placeholder UI in `Home.razor`; a free-form expression text box, TODO/parser comment, or `Calculate` method that sets a fixed/default result is not implementation. The route needs numeric keypad buttons, `+`, `-`, `*`, `/`, `=`, display/result state, divide-by-zero feedback, history, and calls to `CalculatorEngine` operations.");
        builder.AppendLine("- When writing `Home.razor` keypad buttons, use syntax-safe callbacks. Preferred pattern: handlers accept `char` and buttons use `@onclick=\"() => AppendDigit('1')\"` and `@onclick=\"() => ChooseOperator('+')\"`. Alternative pattern: handlers accept `string` and buttons use single-quoted Razor attributes such as `@onclick='() => AppendDigit(\"1\")'`. Do not write `@onclick=\"() => AppendDigit(\"1\")\"`, `@onclick=\"() => SetOperation(\"+\")\"`, `AppendToResult('1')` with a string parameter, or `SetOperation('+')` with a string parameter; these caused prior Razor/CS1503 failures.");
        builder.AppendLine("- If `Calculator.Tests/Calculator.Tests.csproj` already contains `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />`, do not rewrite that project file again until after the routed UI proof passes. The blocker is the effective UI, not the test project file.");
        builder.AppendLine("- If tests fail with `CS0234`, `CS0246`, `Calculator.Domain` missing, or `CalculatorEngine` missing from the sibling test project, the next mutation must repair `Calculator.Tests/Calculator.Tests.csproj` to include `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />` and confirm `Calculator/Domain/CalculatorEngine.cs` exists in namespace `Calculator.Domain`.");
        builder.AppendLine("- If the host build fails with `CS0101` or `CS0111` for `Calculator.Domain.CalculatorEngine`, inspect both `Calculator/CalculatorEngine.cs` and `Calculator/Domain/CalculatorEngine.cs`. Delete stale `Calculator/CalculatorEngine.cs` if both define `CalculatorEngine`; deleting and rewriting only `Domain/CalculatorEngine.cs` does not remove the duplicate type.");
        builder.AppendLine("- Repair `Calculator.Tests/Calculator.Tests.csproj` only when the ProjectReference or test packages are missing; replace or delete the generated empty `UnitTest1.cs`; keep concrete arithmetic tests in the sibling test project.");
        builder.AppendLine("- Replace duplicate add/divide-only tests with one meaningful test source that covers Add, Subtract, Multiply, Divide, and divide-by-zero behavior against `CalculatorEngine`.");
        builder.AppendLine("- After the last source or project-file mutation, read back at least `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator/Domain/CalculatorEngine.cs`, and `Calculator.Tests/Calculator.Tests.csproj`, then run `workspace_dotnet_build` on `Calculator/Calculator.csproj` and `workspace_dotnet_test` on `Calculator.Tests/Calculator.Tests.csproj`.");
        builder.AppendLine("- Write required markdown artifacts only after those build and test commands succeed in this same retry.");
        return builder.ToString();
    }
}
