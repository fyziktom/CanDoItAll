using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ExcelDataReader;
using UglyToad.PdfPig;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public sealed partial class SourceIngestionWorkflowExecutor
{
    private WorkspaceResolvedPath ResolveFile(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveFilePath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source file '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private WorkspaceResolvedPath ResolveDirectory(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source directory '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private string ResolvePathForProbe(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        if (Path.IsPathRooted(path))
        {
            if (!settings.AllowAbsoluteInputPaths)
            {
                return path;
            }

            return Path.GetFullPath(path);
        }

        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false).FullPath;
        }
        catch (InvalidOperationException)
        {
            return path;
        }
    }

}
