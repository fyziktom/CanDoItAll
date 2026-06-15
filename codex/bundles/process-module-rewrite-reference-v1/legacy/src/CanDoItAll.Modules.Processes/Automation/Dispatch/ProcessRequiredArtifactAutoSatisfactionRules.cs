namespace CanDoItAll.Modules.Processes;

internal static class ProcessRequiredArtifactAutoSatisfactionRules
{
    public static bool CanAutoSatisfyRequiredArtifact(
        Func<bool> hasProjectStructureExpectedPath,
        Func<bool> canProjectWorkspaceWrittenArtifact,
        Func<bool> canProjectProcessMockArtifact,
        Func<bool> canProjectProviderNativeVisualArtifact,
        Func<bool> shouldAutoRecordCompletedDecisionArtifact,
        Func<string> resolveProjectableResponseArtifactText,
        Func<(bool HasDeclaredPath, string DeclaredRelativePath)> tryResolveDeclaredPath,
        Func<string, bool> hasProviderNativeBrowserOutputForDeclaredPath,
        Func<string, bool> isResponseProjectableTextArtifact,
        Func<string, bool> isUsableProjectedResponseArtifactContent,
        Func<bool> canProjectResponseTextArtifactWithoutDeclaredPath)
    {
        if (hasProjectStructureExpectedPath())
        {
            return canProjectWorkspaceWrittenArtifact();
        }

        if (canProjectProcessMockArtifact())
        {
            return true;
        }

        if (canProjectWorkspaceWrittenArtifact())
        {
            return true;
        }

        if (canProjectProviderNativeVisualArtifact())
        {
            return true;
        }

        if (shouldAutoRecordCompletedDecisionArtifact())
        {
            return true;
        }

        var projectableResponseText = resolveProjectableResponseArtifactText();
        var declaredPath = tryResolveDeclaredPath();
        if (declaredPath.HasDeclaredPath)
        {
            return hasProviderNativeBrowserOutputForDeclaredPath(declaredPath.DeclaredRelativePath) ||
                   (isUsableProjectedResponseArtifactContent(projectableResponseText) &&
                    isResponseProjectableTextArtifact(declaredPath.DeclaredRelativePath));
        }

        return isUsableProjectedResponseArtifactContent(projectableResponseText) &&
               canProjectResponseTextArtifactWithoutDeclaredPath();
    }
}

