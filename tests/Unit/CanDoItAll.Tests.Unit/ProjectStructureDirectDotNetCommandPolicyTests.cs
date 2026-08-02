using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureDirectDotNetCommandPolicyTests
{
    [Theory]
    [InlineData("dotnet", "watch --project Calculator.csproj run", true)]
    [InlineData("dotnet.exe run", "--project Calculator.csproj", false)]
    [InlineData("& \"C:\\Program Files\\dotnet\\dotnet.exe\"", "watch --project Calculator.csproj run", true)]
    [InlineData("'C:\\Program Files\\dotnet\\dotnet.exe' run", null, false)]
    [InlineData("pwsh", "-NoProfile -Command dotnet watch --project Calculator.csproj run", true)]
    [InlineData("powershell.exe -Command", "\"& 'C:\\Program Files\\dotnet\\dotnet.exe' run --project Calculator.csproj\"", false)]
    [InlineData("cmd", "/c dotnet run --project Calculator.csproj", false)]
    [InlineData("cmd.exe /s /c", "\"dotnet watch --project Calculator.csproj run\"", true)]
    [InlineData("cmd", "/c call dotnet watch --project Calculator.csproj run", true)]
    [InlineData("cmd", "/c start dotnet run --project Calculator.csproj", false)]
    [InlineData("cmd", "/c start \"\" dotnet watch --project Calculator.csproj run", true)]
    [InlineData("cmd", "/c start \"\" /d C:\\programovani\\dotnet\\calculator-e2e-test dotnet watch --project Calculator.csproj run", true)]
    [InlineData("cmd", "/c start \"Calculator\" /d C:\\programovani\\dotnet\\calculator-e2e-test dotnet watch --project Calculator.csproj run", true)]
    [InlineData("cmd", "/c start \"Calculator\" dotnet run --project Calculator.csproj", false)]
    [InlineData("pwsh", "-Command \"Write-Output ready; dotnet watch --project Calculator.csproj run\"", true)]
    [InlineData("pwsh", "-Command Start-Process dotnet -ArgumentList watch,--project,Calculator.csproj,run", true)]
    [InlineData("pwsh", "-Command start dotnet -ArgumentList watch,--project,Calculator.csproj,run", true)]
    [InlineData("start", "dotnet -ArgumentList watch,--project,Calculator.csproj,run", true)]
    [InlineData("pwsh", "-Command Start-Process -WorkingDirectory C:\\programovani\\dotnet\\calculator-e2e-test dotnet -ArgumentList watch,--project,Calculator.csproj,run", true)]
    [InlineData("powershell", "-Command \"Start-Process -FilePath dotnet -ArgumentList 'run','--project','Calculator.csproj'\"", false)]
    [InlineData("cmd", "/c \"echo ready && dotnet run --project Calculator.csproj\"", false)]
    public void TryClassify_recognizes_direct_dotnet_run_and_watch_commands(
        string command,
        string? arguments,
        bool isWatch)
    {
        var classified = ProjectStructureDirectDotNetCommandPolicy.TryClassify(
            command,
            arguments,
            out var commandKind);

        Assert.True(classified);
        Assert.Equal(
            isWatch ? ProjectStructureDirectDotNetCommandKind.Watch : ProjectStructureDirectDotNetCommandKind.Run,
            commandKind);
    }

    [Theory]
    [InlineData("dotnet", "test Calculator.csproj")]
    [InlineData("& \"C:\\Program Files\\dotnet\\dotnet.exe", "watch")]
    [InlineData("pwsh", "-File scripts\\start.ps1")]
    [InlineData("cmd", "/c echo dotnet watch")]
    [InlineData("cmd", "/c start dotnet test Calculator.csproj")]
    [InlineData("cmd", "/c start Calculator /d C:\\apps\\calc dotnet watch --project Calculator.csproj run")]
    [InlineData("cmd", "/c start dotnet -ArgumentList watch,--project,Calculator.csproj,run")]
    [InlineData("cmd", "/c start \"dotnet\" -ArgumentList watch,--project,Calculator.csproj,run")]
    [InlineData("pwsh", "-Command Start-Process dotnet -ArgumentList test,Calculator.csproj")]
    [InlineData("pwsh", "-Command Start-Process $runtime -ArgumentList watch,--project,Calculator.csproj")]
    [InlineData("pwsh", "-Command Start-Process -WorkingDirectory dotnet python -ArgumentList watch,--project,Calculator.csproj")]
    [InlineData("pwsh", "-EncodedCommand ZABvAHQAbgBlAHQAIAB3AGEAdABjAGgA")]
    public void TryClassify_does_not_claim_wrapped_non_runtime_or_malformed_commands(
        string command,
        string? arguments)
    {
        Assert.False(ProjectStructureDirectDotNetCommandPolicy.TryClassify(
            command,
            arguments,
            out _));
    }
}
