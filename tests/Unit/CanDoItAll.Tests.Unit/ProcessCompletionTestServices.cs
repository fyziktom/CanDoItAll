using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.Tests.Unit;

internal static class ProcessCompletionTestServices
{
    internal static ProcessCompletionIssueResultFactory CreateIssueResultFactory(
        IWorkspaceFileService workspaceFiles,
        ProcessCompletionDefectEvidenceCatalog completionDefectEvidenceCatalog,
        WorkspaceFileInspectionScopeFactory? workspaceFileInspectionScopeFactory = null)
    {
        workspaceFileInspectionScopeFactory ??= new WorkspaceFileInspectionScopeFactory(
            Path.GetTempPath(),
            WorkspaceScopeDescriptor.Sandbox,
            TestWorkspaceServices.PhysicalPathPolicyFactory,
            new ExternalTargetPathRegistryFactory(new EphemeralDataProtectionProvider()));
        var filesystemInspector = new ProcessProductFilesystemInspector(
            workspaceFileInspectionScopeFactory);
        return new ProcessCompletionIssueResultFactory(
            workspaceFiles,
            completionDefectEvidenceCatalog,
            new ProcessProductCompletionPathGate(filesystemInspector));
    }
}
