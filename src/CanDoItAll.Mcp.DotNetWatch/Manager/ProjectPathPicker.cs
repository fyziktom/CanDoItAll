using System.Diagnostics;
using System.Text;

namespace CanDoItAll.Mcp.DotNetWatch.Manager;

internal interface IProjectPathPicker
{
    Task<string?> PickProjectPathAsync(string initialDirectory, CancellationToken cancellationToken);
}

internal sealed class WindowsProjectPathPicker(ILogger<WindowsProjectPathPicker> logger) : IProjectPathPicker
{
    public async Task<string?> PickProjectPathAsync(string initialDirectory, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ToolInvocationException("UnsupportedAction", "Project browsing is currently supported only on Windows.");
        }

        var effectiveInitialDirectory = Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var escapedInitialDirectory = effectiveInitialDirectory.Replace("'", "''", StringComparison.Ordinal);
        var command = $$"""
Add-Type -AssemblyName System.Windows.Forms;
$dialog = New-Object System.Windows.Forms.OpenFileDialog;
$dialog.Filter = 'C# Projects (*.csproj)|*.csproj|All Files (*.*)|*.*';
$dialog.Multiselect = $false;
$dialog.CheckFileExists = $true;
$dialog.CheckPathExists = $true;
$dialog.InitialDirectory = '{{escapedInitialDirectory}}';
if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
    [Console]::Out.Write($dialog.FileName);
}
""";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-STA");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add(command);

        if (!process.Start())
        {
            throw new ToolInvocationException("ProjectBrowseFailed", "Could not start the Windows file picker process.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var selectedPath = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();
        if (process.ExitCode != 0)
        {
            logger.LogWarning("Project picker exited with code {ExitCode}: {Error}", process.ExitCode, stderr);
            throw new ToolInvocationException("ProjectBrowseFailed", $"The Windows file picker failed with exit code {process.ExitCode}.");
        }

        return string.IsNullOrWhiteSpace(selectedPath)
            ? null
            : Path.GetFullPath(selectedPath);
    }
}
