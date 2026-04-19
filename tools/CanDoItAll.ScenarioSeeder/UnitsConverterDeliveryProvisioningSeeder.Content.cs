using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class UnitsConverterDeliveryProvisioningSeeder
{
    private static readonly string[] UiValidationStepKeys =
    [
        "implementation",
        "qa-validation",
        "ui-review",
        "execute-release-rollout"
    ];

    private UnitsConverterWorkspacePlan EnsureWorkspaceAssets()
    {
        var organizationScope = workspaceFactory.GetOrganizationScope();
        var deliveryRoot = ResolveWorkspaceFullPath(DeliveryRootRelativePath, organizationScope);
        var artifactRoot = ResolveWorkspaceFullPath(DeliveryArtifactRootRelativePath, organizationScope);
        var legacyDeliveryRoot = ResolveWorkspaceFullPath(LegacyDeliveryRootRelativePath, organizationScope);
        var legacyArtifactRoot = ResolveWorkspaceFullPath(LegacyDeliveryArtifactRootRelativePath, organizationScope);
        var processEvidenceRoot = ResolveWorkspaceFullPath(ProcessEvidenceRelativePath, organizationScope);
        var uiEvidenceRoot = ResolveWorkspaceFullPath(UiEvidenceRelativePath, organizationScope);
        var playwrightScratchRoot = ResolveWorkspaceFullPath(PlaywrightScratchRelativePath, organizationScope);
        var legacyPlaywrightScratchRoot = ResolveWorkspaceFullPath(LegacyPlaywrightScratchRelativePath, organizationScope);
        var pidFile = Path.Combine(uiEvidenceRoot, "units-app.pid");
        var legacyPidFile = Path.Combine(
            Path.Combine(
                options.WorkspaceRootPath,
                ResolveScopedManagedRelativePath(LegacyDeliveryArtifactRootRelativePath, organizationScope)
                    .Replace('/', Path.DirectorySeparatorChar)),
            "ui",
            "units-app.pid");

        TryStopDeliveryProcess(pidFile);
        TryStopDeliveryProcess(legacyPidFile);
        DeleteDirectoryIfExists(legacyArtifactRoot);
        DeleteDirectoryIfExists(legacyDeliveryRoot);
        DeleteDirectoryIfExists(legacyPlaywrightScratchRoot);
        DeleteDirectoryIfExists(deliveryRoot);
        DeleteDirectoryIfExists(playwrightScratchRoot);
        ResetDirectory(artifactRoot);

        Directory.CreateDirectory(deliveryRoot);
        Directory.CreateDirectory(Path.Combine(deliveryRoot, "src"));
        Directory.CreateDirectory(Path.Combine(deliveryRoot, "tests"));
        Directory.CreateDirectory(processEvidenceRoot);
        Directory.CreateDirectory(uiEvidenceRoot);
        Directory.CreateDirectory(playwrightScratchRoot);
        foreach (var stepKey in UiValidationStepKeys)
        {
            Directory.CreateDirectory(Path.Combine(playwrightScratchRoot, stepKey));
            Directory.CreateDirectory(Path.Combine(uiEvidenceRoot, stepKey));
            Directory.CreateDirectory(Path.Combine(processEvidenceRoot, stepKey));
        }

        var plan = new UnitsConverterWorkspacePlan(
            deliveryRoot,
            artifactRoot,
            processEvidenceRoot,
            uiEvidenceRoot,
            ResolveWorkspaceFullPath(BriefRelativePath, organizationScope),
            ResolveWorkspaceFullPath(BootstrapScriptRelativePath, organizationScope),
            ResolveWorkspaceFullPath(LaunchScriptRelativePath, organizationScope),
            ResolveWorkspaceFullPath(StopScriptRelativePath, organizationScope),
            ResolveWorkspaceFullPath(ImportPlaywrightEvidenceScriptRelativePath, organizationScope),
            playwrightScratchRoot,
            ResolveWorkspaceFullPath(SolutionRelativePath, organizationScope),
            ResolveWorkspaceFullPath(WebProjectRelativePath, organizationScope),
            ResolveWorkspaceFullPath(CoreProjectRelativePath, organizationScope),
            ResolveWorkspaceFullPath(TestsProjectRelativePath, organizationScope));

        File.WriteAllText(plan.BriefFullPath, BuildBriefContent(plan), new UTF8Encoding(false));
        File.WriteAllText(plan.BootstrapScriptFullPath, BuildBootstrapScriptContent(), new UTF8Encoding(false));
        File.WriteAllText(plan.LaunchScriptFullPath, BuildLaunchScriptContent(), new UTF8Encoding(false));
        File.WriteAllText(plan.StopScriptFullPath, BuildStopScriptContent(), new UTF8Encoding(false));
        File.WriteAllText(plan.ImportPlaywrightEvidenceScriptFullPath, BuildImportPlaywrightEvidenceScriptContent(), new UTF8Encoding(false));
        return plan;
    }

    private string BuildBriefContent(UnitsConverterWorkspacePlan workspacePlan)
    {
        return new StringBuilder()
            .AppendLine("# Blazor SSR basic units converter")
            .AppendLine()
            .AppendLine("This is a serious governed delivery project. The application must be produced by the CanDoItAll agents bound through AgentFramework, with human-controlled scope and release gates.")
            .AppendLine()
            .AppendLine("## Required outcome")
            .AppendLine($"- Solution path: `{SolutionRelativePath}`")
            .AppendLine($"- Core project: `{CoreProjectRelativePath}`")
            .AppendLine($"- Web project: `{WebProjectRelativePath}`")
            .AppendLine($"- Tests project: `{TestsProjectRelativePath}`")
            .AppendLine($"- Launch URL: `{AppUrl}`")
            .AppendLine()
            .AppendLine("## Functional scope")
            .AppendLine("- Support length, mass, temperature, and volume conversions.")
            .AppendLine("- Provide explicit source unit, target unit, value input, converted result, and validation feedback.")
            .AppendLine("- Keep the domain logic typed and maintainable rather than pushing conversion rules into the UI.")
            .AppendLine("- Include automated tests for representative conversions and invalid-input behavior.")
            .AppendLine()
            .AppendLine("## Delivery rules")
            .AppendLine("- Use the bootstrap script only to scaffold the blank solution and project layout.")
            .AppendLine("- After bootstrap, read the generated `.csproj`, `Program.cs`, page, and test files before making substantial edits.")
            .AppendLine("- Keep the on-disk solution, project, and folder names short because the managed workspace root is deep enough to trigger Windows path-length failures when app identifiers are unnecessarily verbose.")
            .AppendLine("- Preserve the scaffolded .NET 10 Blazor SSR and MSTest shape unless the approved scope explicitly requires a different target.")
            .AppendLine("- If the generated test project references the modern `MSTest` package, use its current assertion APIs such as `Assert.Throws<T>` instead of legacy `Assert.ThrowsException(...)` or `[ExpectedException]` patterns.")
            .AppendLine("- Do not use `object`, `dynamic`, or other weakly typed bind targets for Blazor form state. Use explicit view-model properties or enums so the app cannot fail at runtime with TypeConverter or ambiguous binding errors.")
            .AppendLine("- The final application code must come from the assigned agents, not from a prewritten final-code helper.")
            .AppendLine("- Product scope confirmation, release approval, and post-release learning remain human-controlled.")
            .AppendLine("- Code review, QA, UI review, security review, and rollout are separate lanes and must leave durable evidence.")
            .AppendLine("- Remove the stock Counter and Weather template flows and replace scaffold navigation or placeholder home content with the real units-converter surface.")
            .AppendLine("- Replace the stock `MainLayout`, `NavMenu`, and default documentation-oriented scaffold shell with product-specific navigation and layout.")
            .AppendLine("- Do not leave Microsoft Docs links, default sidebar chrome, or mostly unchanged scaffold `app.css` content on the shipped surface.")
            .AppendLine("- The implementation lane must prove the app starts and renders successfully before handing the build to QA. Startup or render crashes stay in implementation until fixed.")
            .AppendLine()
            .AppendLine("## UX bar")
            .AppendLine("- The UI must read as intentional rather than default-template output.")
            .AppendLine("- The primary `/` route must feel like the actual product surface, not a stock scaffold or a temporary link list.")
            .AppendLine("- Desktop and mobile states must both be reviewable through screenshots.")
            .AppendLine("- At least one screenshot-backed proof state must show a successful conversion with the entered value, source unit, target unit, and visible result.")
            .AppendLine("- Copy must be clear for a non-technical user who only wants a correct conversion quickly.")
            .AppendLine()
            .AppendLine("## Evidence destinations")
            .AppendLine($"- Process evidence root: `{ProcessEvidenceRelativePath}`")
            .AppendLine($"- Managed UI evidence root: `{BuildScopedUiEvidenceRelativePath()}`")
            .AppendLine($"- UI evidence filesystem root: `{workspacePlan.UiEvidenceFullPath}`")
            .AppendLine()
            .AppendLine("## Explicit exclusions")
            .AppendLine("- Do not turn this into a multi-page admin platform.")
            .AppendLine("- Do not introduce a second source of truth for agents.")
            .AppendLine("- Do not rely on hidden human fallback to cover missing artifacts or skipped validation.")
            .ToString();
    }

    private string BuildBootstrapScriptContent()
    {
        return $$"""
$ErrorActionPreference = 'Stop'

$deliveryRoot = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(DeliveryRootRelativePath, workspaceFactory.GetOrganizationScope()))}}'
$solutionPath = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(SolutionRelativePath, workspaceFactory.GetOrganizationScope()))}}'
$coreProjectRoot = '{{ToPowerShellLiteral(Path.GetDirectoryName(ResolveWorkspaceFullPath(CoreProjectRelativePath, workspaceFactory.GetOrganizationScope())) ?? string.Empty)}}'
$webProjectRoot = '{{ToPowerShellLiteral(Path.GetDirectoryName(ResolveWorkspaceFullPath(WebProjectRelativePath, workspaceFactory.GetOrganizationScope())) ?? string.Empty)}}'
$testsProjectRoot = '{{ToPowerShellLiteral(Path.GetDirectoryName(ResolveWorkspaceFullPath(TestsProjectRelativePath, workspaceFactory.GetOrganizationScope())) ?? string.Empty)}}'
$coreProject = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(CoreProjectRelativePath, workspaceFactory.GetOrganizationScope()))}}'
$webProject = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(WebProjectRelativePath, workspaceFactory.GetOrganizationScope()))}}'
$testsProject = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(TestsProjectRelativePath, workspaceFactory.GetOrganizationScope()))}}'

New-Item -ItemType Directory -Force -Path $deliveryRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $deliveryRoot 'src') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $deliveryRoot 'tests') | Out-Null

if (-not (Test-Path -LiteralPath $solutionPath)) {
    & dotnet new sln --format slnx --name Units --output $deliveryRoot | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to scaffold solution at $solutionPath."
    }
}

if (-not (Test-Path -LiteralPath $coreProject)) {
    & dotnet new classlib --name Units.Core --framework net10.0 --output $coreProjectRoot | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to scaffold core project at $coreProjectRoot."
    }
}

if (-not (Test-Path -LiteralPath $webProject)) {
    & dotnet new blazor --name Units.Web --framework net10.0 --output $webProjectRoot | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to scaffold web project at $webProjectRoot."
    }
}

if (-not (Test-Path -LiteralPath $testsProject)) {
    & dotnet new mstest --name Units.Tests --framework net10.0 --output $testsProjectRoot | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to scaffold tests project at $testsProjectRoot."
    }
}

$defaultCoreClass = Join-Path $coreProjectRoot 'Class1.cs'
if (Test-Path -LiteralPath $defaultCoreClass) {
    Remove-Item -LiteralPath $defaultCoreClass -Force
}

foreach ($defaultTestFile in @('UnitTest1.cs', 'Test1.cs')) {
    $defaultTestClass = Join-Path $testsProjectRoot $defaultTestFile
    if (Test-Path -LiteralPath $defaultTestClass) {
        Remove-Item -LiteralPath $defaultTestClass -Force
    }
}

& dotnet sln $solutionPath add $coreProject | Out-Null
& dotnet sln $solutionPath add $webProject | Out-Null
& dotnet sln $solutionPath add $testsProject | Out-Null
& dotnet add $webProject reference $coreProject | Out-Null
& dotnet add $testsProject reference $coreProject | Out-Null
& dotnet restore $solutionPath --nologo | Out-Null

Write-Output "Blank units-converter solution scaffolded at $solutionPath"
""";
    }

    private string BuildLaunchScriptContent()
    {
        return $$"""
$ErrorActionPreference = 'Stop'

$webProject = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(WebProjectRelativePath, workspaceFactory.GetOrganizationScope()))}}'
$webProjectRoot = Split-Path -Parent $webProject
$uiEvidenceRoot = '{{ToPowerShellLiteral(BuildUiEvidenceStepFullPath(string.Empty).TrimEnd('\\', '/'))}}'
$pidFile = Join-Path $uiEvidenceRoot 'units-app.pid'
$stdoutLog = Join-Path $uiEvidenceRoot 'units-app.stdout.log'
$stderrLog = Join-Path $uiEvidenceRoot 'units-app.stderr.log'
$buildLog = Join-Path $uiEvidenceRoot 'units-app.build.log'
$runtimeRoot = Join-Path $uiEvidenceRoot 'runtime'
$publishedAppRoot = Join-Path $runtimeRoot 'current'
$appUrl = '{{AppUrl}}'

if (-not (Test-Path -LiteralPath $webProject)) {
    throw "Web project not found at $webProject."
}

New-Item -ItemType Directory -Force -Path $uiEvidenceRoot | Out-Null
New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null

if (Test-Path -LiteralPath $pidFile) {
    $existingPidText = Get-Content -LiteralPath $pidFile -Raw
    $existingPid = 0
    if ([int]::TryParse($existingPidText.Trim(), [ref]$existingPid)) {
        $existingProcess = Get-Process -Id $existingPid -ErrorAction SilentlyContinue
        if ($null -ne $existingProcess) {
            Stop-Process -Id $existingPid -Force
            Start-Sleep -Seconds 1
        }
    }
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
}

if (Test-Path -LiteralPath $publishedAppRoot) {
    Remove-Item -LiteralPath $publishedAppRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$buildOutput = & dotnet publish $webProject --nologo -c Debug -o $publishedAppRoot 2>&1
$buildOutput | Set-Content -LiteralPath $buildLog -Encoding utf8NoBOM
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $webProject. Review $buildLog."
}

$projectDirectory = $publishedAppRoot

$appName = [System.IO.Path]::GetFileNameWithoutExtension($webProject)
$appExecutable = Join-Path $publishedAppRoot ($appName + '.exe')
$appDll = Join-Path $publishedAppRoot ($appName + '.dll')

if (-not (Test-Path -LiteralPath $appExecutable) -and -not (Test-Path -LiteralPath $appDll)) {
    $availableOutputs = if (Test-Path -LiteralPath $publishedAppRoot) {
        (Get-ChildItem -LiteralPath $publishedAppRoot -Force | Select-Object -ExpandProperty Name) -join ', '
    }
    else {
        'none'
    }

    throw "Published units-converter host was not found under $publishedAppRoot. Available outputs: $availableOutputs. Review $buildLog."
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'

$launchFilePath = $appExecutable
$launchArguments = @(
    '--urls', $appUrl
)
if (-not (Test-Path -LiteralPath $launchFilePath)) {
    $launchFilePath = 'dotnet'
    $launchArguments = @(
        $appDll,
        '--urls',
        $appUrl
    )
}

$process = Start-Process `
    -FilePath $launchFilePath `
    -ArgumentList $launchArguments `
    -WorkingDirectory $projectDirectory `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog `
    -PassThru
$process.Id | Set-Content -LiteralPath $pidFile -Encoding utf8NoBOM

$ready = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    Start-Sleep -Seconds 1

    if ($process.HasExited) {
        break
    }

    try {
        $response = Invoke-WebRequest -Uri $appUrl -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
            $ready = $true
            break
        }
    }
    catch {
    }
}

if (-not $ready) {
    if ($process.HasExited) {
        Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
        throw "Units-converter app exited before becoming ready. Review $stdoutLog and $stderrLog."
    }

    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
    throw "Units-converter app did not become reachable at $appUrl. Review $stdoutLog and $stderrLog."
}

Write-Output "Units-converter app is running at $appUrl"
""";
    }

    private string BuildStopScriptContent()
    {
        return $$"""
$ErrorActionPreference = 'Stop'

$pidFile = '{{ToPowerShellLiteral(Path.Combine(BuildUiEvidenceStepFullPath(string.Empty).TrimEnd('\\', '/'), "units-app.pid"))}}'
if (-not (Test-Path -LiteralPath $pidFile)) {
    Write-Output 'Units-converter app is not running.'
    return
}

$pidText = Get-Content -LiteralPath $pidFile -Raw
$pidValue = 0
if (-not [int]::TryParse($pidText.Trim(), [ref]$pidValue)) {
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
    throw "Stored PID '$pidText' is invalid."
}

$process = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
if ($null -ne $process) {
    Stop-Process -Id $pidValue -Force
}

Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
Write-Output 'Units-converter app stopped.'
""";
    }

    private string BuildImportPlaywrightEvidenceScriptContent()
    {
        return $$"""
param(
    [Parameter(Mandatory = $true)]
    [string]$StepKey
)

$ErrorActionPreference = 'Stop'

$deliveryRoot = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(DeliveryRootRelativePath, workspaceFactory.GetOrganizationScope()))}}'
$workspaceRoot = '{{ToPowerShellLiteral(options.WorkspaceRootPath)}}'
$playwrightRoot = '{{ToPowerShellLiteral(ResolveWorkspaceFullPath(PlaywrightScratchRelativePath, workspaceFactory.GetOrganizationScope()))}}'
$uiEvidenceRoot = '{{ToPowerShellLiteral(BuildUiEvidenceStepFullPath(string.Empty).TrimEnd('\\', '/'))}}'
$targetRoot = Join-Path $uiEvidenceRoot $StepKey
$candidateRoots = @(
    (Join-Path $playwrightRoot $StepKey),
    (Join-Path $deliveryRoot $StepKey),
    (Join-Path $workspaceRoot $StepKey),
    $playwrightRoot
) | Where-Object { Test-Path -LiteralPath $_ }

if ($candidateRoots.Count -eq 0) {
    throw "Playwright evidence roots were not found for step '$StepKey'."
}

$expectedFiles = switch ($StepKey) {
    'qa-validation' { @('desktop-home.png', 'desktop-representative-conversion.png', 'page.yml', 'console.log') }
    'ui-review' { @('desktop-home.png', 'mobile-home.png', 'page.yml', 'console.log') }
    'execute-release-rollout' { @('release-home.png', 'page.yml', 'console.log') }
    default { @('page.yml', 'console.log') }
}

New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
$copiedFiles = @()

foreach ($expectedFile in $expectedFiles) {
    $sourceFile = $null
    foreach ($candidateRoot in $candidateRoots) {
        $match = Get-ChildItem -LiteralPath $candidateRoot -File -Recurse -Force | Where-Object {
            $_.Name -ieq $expectedFile
        } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if ($null -ne $match) {
            $sourceFile = $match
            break
        }
    }

    if ($null -eq $sourceFile) {
        throw "Expected Playwright evidence file '$expectedFile' was not found for step '$StepKey'."
    }

    $targetPath = Join-Path $targetRoot $expectedFile
    Copy-Item -LiteralPath $sourceFile.FullName -Destination $targetPath -Force
    $copiedFiles += $targetPath
}

$summaryPath = Join-Path $targetRoot 'import-summary.json'
$summary = [ordered]@{
    stepKey = $StepKey
    copiedFiles = $copiedFiles
    importedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM

Write-Output "Imported Playwright evidence for step '$StepKey' into $targetRoot"
""";
    }

    private string ResolveWorkspaceFullPath(string relativePath, WorkspaceScopeDescriptor scope)
    {
        var resolvedRelativePath = ResolveScopedManagedRelativePath(relativePath, scope);
        return Path.Combine(
            options.WorkspaceRootPath,
            resolvedRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ResolveScopedManagedRelativePath(string relativePath, WorkspaceScopeDescriptor scope)
    {
        if (!relativePath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        var artifactSuffix = relativePath["artifacts/".Length..];
        return scope.CombineArtifactPath(artifactSuffix);
    }

    private string BuildScopedUiEvidenceRelativePath()
    {
        return ResolveScopedManagedRelativePath(
            UiEvidenceRelativePath,
            workspaceFactory.GetOrganizationScope());
    }

    private string BuildUiEvidenceStepFullPath(string stepKey)
    {
        var relativePath = string.IsNullOrWhiteSpace(stepKey)
            ? BuildScopedUiEvidenceRelativePath()
            : $"{BuildScopedUiEvidenceRelativePath()}/{stepKey}";
        return Path.Combine(
            options.WorkspaceRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string BuildUiImportOutputPathArgument(string stepKey)
    {
        return string.Join(
            ", ",
            BuildUiExpectedRelativeFiles(stepKey).Select(item => $"'{BuildScopedUiEvidenceRelativePath()}/{stepKey}/{item}'"));
    }

    private static string BuildPlaywrightScratchCaptureRelativePath(string stepKey, string fileName)
    {
        return $"{PlaywrightScratchRelativePath}/{stepKey}/{fileName}";
    }

    private static IReadOnlyList<string> BuildUiExpectedRelativeFiles(string stepKey)
    {
        return stepKey switch
        {
            "implementation" => ["desktop-home.png", "page.yml", "console.log", "import-summary.json"],
            "qa-validation" => ["desktop-home.png", "desktop-representative-conversion.png", "page.yml", "console.log", "import-summary.json"],
            "ui-review" => ["desktop-home.png", "mobile-home.png", "page.yml", "console.log", "import-summary.json"],
            "execute-release-rollout" => ["release-home.png", "page.yml", "console.log", "import-summary.json"],
            _ => ["page.yml", "console.log", "import-summary.json"]
        };
    }

    private void TryStopDeliveryProcess(string pidFile)
    {
        if (!File.Exists(pidFile))
        {
            return;
        }

        try
        {
            var pidText = File.ReadAllText(pidFile).Trim();
            if (!int.TryParse(pidText, out var pid))
            {
                return;
            }

            var process = System.Diagnostics.Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            try
            {
                File.Delete(pidFile);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void ResetDirectory(string path)
    {
        DeleteDirectoryIfExists(path);
        Directory.CreateDirectory(path);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string ToPowerShellLiteral(string path)
    {
        return path.Replace("'", "''", StringComparison.Ordinal);
    }

    private sealed record UnitsConverterWorkspacePlan(
        string DeliveryRootFullPath,
        string ArtifactRootFullPath,
        string ProcessEvidenceFullPath,
        string UiEvidenceFullPath,
        string BriefFullPath,
        string BootstrapScriptFullPath,
        string LaunchScriptFullPath,
        string StopScriptFullPath,
        string ImportPlaywrightEvidenceScriptFullPath,
        string PlaywrightScratchFullPath,
        string SolutionFullPath,
        string WebProjectFullPath,
        string CoreProjectFullPath,
        string TestsProjectFullPath);
}
