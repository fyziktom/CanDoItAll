using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal static class DotNetSolutionSetupScriptFactory
{
    public static string BuildCreateProjectSideEffectManifest(DotNetProcessLaunchContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var manifest = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = contract.SolutionCandidatePaths.Concat([contract.AppProjectFile]).ToArray(),
            ["declaredWritePaths"] = contract.SolutionCandidatePaths,
            ["allowShellDelegation"] = true
        };

        return JsonSerializer.Serialize(manifest);
    }

    public static string BuildAddTestProjectSideEffectManifest(DotNetProcessLaunchContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var manifest = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = contract.SolutionCandidatePaths.Concat([contract.AppProjectFile, contract.TestProjectFile]).ToArray(),
            ["declaredWritePaths"] = contract.SolutionCandidatePaths.Concat([contract.TestProjectDirectory, contract.TestProjectFile]).ToArray(),
            ["allowShellDelegation"] = true
        };

        return JsonSerializer.Serialize(manifest);
    }

    public static string BuildCreateProjectScript(DotNetProcessLaunchContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var solutionArray = string.Join(", ", contract.SolutionCandidatePaths.Select(ToPowerShellSingleQuoted));
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine($"$ProductRoot = {ToPowerShellSingleQuoted(contract.ProductRoot)}");
        builder.AppendLine($"$SolutionCandidates = @({solutionArray})");
        builder.AppendLine($"$AppProjectFile = {ToPowerShellSingleQuoted(contract.AppProjectFile)}");
        builder.AppendLine();
        AppendPowerShellSupportFunctions(builder);
        builder.AppendLine("$SolutionFile = $SolutionCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($SolutionFile)) {");
        builder.AppendLine("    throw \"No contracted solution file exists. Candidates: $($SolutionCandidates -join '; ')\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $AppProjectFile -PathType Leaf)) {");
        builder.AppendLine("    throw \"Contracted app project file is missing: $AppProjectFile\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-SolutionContainsProject $SolutionFile $AppProjectFile)) {");
        builder.AppendLine("    Invoke-Dotnet @('sln', $SolutionFile, 'add', $AppProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine("$finalListText = Get-SolutionListText $SolutionFile");
        builder.AppendLine("$finalNormalizedList = Normalize-PathText $finalListText");
        builder.AppendLine("$appRelative = Normalize-PathText ([System.IO.Path]::GetRelativePath($ProductRoot, $AppProjectFile))");
        builder.AppendLine("if (-not $finalNormalizedList.Contains($appRelative)) {");
        builder.AppendLine("    throw \"Solution membership is missing the app project relative path: $appRelative\"");
        builder.AppendLine("}");
        builder.AppendLine("Write-Host \"Verified solution membership for $AppProjectFile.\"");
        return builder.ToString();
    }

    public static string BuildAddTestProjectScript(DotNetProcessLaunchContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var solutionArray = string.Join(", ", contract.SolutionCandidatePaths.Select(ToPowerShellSingleQuoted));
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine($"$ProductRoot = {ToPowerShellSingleQuoted(contract.ProductRoot)}");
        builder.AppendLine($"$SolutionCandidates = @({solutionArray})");
        builder.AppendLine($"$AppProjectFile = {ToPowerShellSingleQuoted(contract.AppProjectFile)}");
        builder.AppendLine($"$TestProjectFile = {ToPowerShellSingleQuoted(contract.TestProjectFile)}");
        builder.AppendLine($"$TestProjectName = {ToPowerShellSingleQuoted(contract.TestProjectName)}");
        builder.AppendLine($"$TestProjectDirectory = {ToPowerShellSingleQuoted(contract.TestProjectDirectory)}");
        builder.AppendLine($"$TestTemplate = {ToPowerShellSingleQuoted(contract.TestTemplate)}");
        builder.AppendLine($"$TargetFramework = {ToPowerShellSingleQuoted(contract.TargetFramework)}");
        builder.AppendLine();
        AppendPowerShellSupportFunctions(builder);
        builder.AppendLine("$SolutionFile = $SolutionCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1");
        builder.AppendLine("if ([string]::IsNullOrWhiteSpace($SolutionFile)) {");
        builder.AppendLine("    throw \"No contracted solution file exists. Candidates: $($SolutionCandidates -join '; ')\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $AppProjectFile -PathType Leaf)) {");
        builder.AppendLine("    throw \"Contracted app project file is missing: $AppProjectFile\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $TestProjectFile -PathType Leaf)) {");
        builder.AppendLine("    $testProjectParentDirectory = Split-Path -Parent $TestProjectDirectory");
        builder.AppendLine("    if (-not (Test-Path -LiteralPath $testProjectParentDirectory -PathType Container)) {");
        builder.AppendLine("        New-Item -ItemType Directory -Path $testProjectParentDirectory -Force | Out-Null");
        builder.AppendLine("    }");
        builder.AppendLine("    $newTestProjectArguments = @('new', $TestTemplate, '--name', $TestProjectName, '--output', $TestProjectDirectory)");
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($TargetFramework)) {");
        builder.AppendLine("        $newTestProjectArguments += @('--framework', $TargetFramework)");
        builder.AppendLine("    }");
        builder.AppendLine("    Invoke-Dotnet $newTestProjectArguments | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-Path -LiteralPath $TestProjectFile -PathType Leaf)) {");
        builder.AppendLine("    throw \"Contracted test project file is missing: $TestProjectFile\"");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("if (-not (Test-SolutionContainsProject $SolutionFile $AppProjectFile)) {");
        builder.AppendLine("    Invoke-Dotnet @('sln', $SolutionFile, 'add', $AppProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine("if (-not (Test-SolutionContainsProject $SolutionFile $TestProjectFile)) {");
        builder.AppendLine("    Invoke-Dotnet @('sln', $SolutionFile, 'add', $TestProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("$testProjectDirectory = Split-Path -Parent $TestProjectFile");
        builder.AppendLine("$expectedReference = [System.IO.Path]::GetRelativePath($testProjectDirectory, $AppProjectFile)");
        builder.AppendLine("$testProjectText = Get-Content -LiteralPath $TestProjectFile -Raw");
        builder.AppendLine("if (-not (Normalize-PathText $testProjectText).Contains((Normalize-PathText $expectedReference))) {");
        builder.AppendLine("    Invoke-Dotnet @('add', $TestProjectFile, 'reference', $AppProjectFile) | Out-Null");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("$finalListText = Get-SolutionListText $SolutionFile");
        builder.AppendLine("$finalNormalizedList = Normalize-PathText $finalListText");
        builder.AppendLine("$appRelative = Normalize-PathText ([System.IO.Path]::GetRelativePath($ProductRoot, $AppProjectFile))");
        builder.AppendLine("$testRelative = Normalize-PathText ([System.IO.Path]::GetRelativePath($ProductRoot, $TestProjectFile))");
        builder.AppendLine("if (-not $finalNormalizedList.Contains($appRelative)) {");
        builder.AppendLine("    throw \"Solution membership is missing the app project relative path: $appRelative\"");
        builder.AppendLine("}");
        builder.AppendLine("if (-not $finalNormalizedList.Contains($testRelative)) {");
        builder.AppendLine("    throw \"Solution membership is missing the test project relative path: $testRelative\"");
        builder.AppendLine("}");
        builder.AppendLine("$finalTestProjectText = Get-Content -LiteralPath $TestProjectFile -Raw");
        builder.AppendLine("if (-not (Normalize-PathText $finalTestProjectText).Contains((Normalize-PathText $expectedReference))) {");
        builder.AppendLine("    throw \"Test project is missing ProjectReference relative path: $expectedReference\"");
        builder.AppendLine("}");
        builder.AppendLine("Write-Host \"Verified solution membership and ProjectReference for $TestProjectFile.\"");
        return builder.ToString();
    }

    private static void AppendPowerShellSupportFunctions(StringBuilder builder)
    {
        builder.AppendLine("function Normalize-PathText([string]$Value) {");
        builder.AppendLine("    if ($null -eq $Value) { return '' }");
        builder.AppendLine("    return $Value.Replace('\\', '/').ToLowerInvariant()");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Invoke-Dotnet([string[]]$Arguments) {");
        builder.AppendLine("    $output = & dotnet @Arguments 2>&1");
        builder.AppendLine("    $text = $output -join [Environment]::NewLine");
        builder.AppendLine("    if ($LASTEXITCODE -ne 0) {");
        builder.AppendLine("        throw \"dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE. $text\"");
        builder.AppendLine("    }");
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($text)) { Write-Host $text }");
        builder.AppendLine("    return $text");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Get-SolutionListText([string]$SolutionFile) {");
        builder.AppendLine("    return Invoke-Dotnet @('sln', $SolutionFile, 'list')");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Test-SolutionContainsProject([string]$SolutionFile, [string]$ProjectFile) {");
        builder.AppendLine("    $listText = Get-SolutionListText $SolutionFile");
        builder.AppendLine("    $relativeProjectPath = [System.IO.Path]::GetRelativePath($ProductRoot, $ProjectFile)");
        builder.AppendLine("    $normalizedList = Normalize-PathText $listText");
        builder.AppendLine("    $normalizedRelative = Normalize-PathText $relativeProjectPath");
        builder.AppendLine("    return $normalizedList.Contains($normalizedRelative)");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static string ToPowerShellSingleQuoted(string value)
        => $"'{value.Replace("'", "''")}'";
}
