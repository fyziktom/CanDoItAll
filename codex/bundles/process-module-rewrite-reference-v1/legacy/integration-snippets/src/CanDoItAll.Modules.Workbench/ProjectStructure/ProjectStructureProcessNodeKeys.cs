namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessNodeKeys
{
    public const string ProcessDefinitionPrefix = "process-definition:";
    public const string ProcessRunPrefix = "process-run:";
    public const string ProcessRunOutputPrefix = "process-run-output:";

    public static string BuildProcessDefinitionNodeKey(Guid definitionId)
    {
        return $"{ProcessDefinitionPrefix}{definitionId:D}";
    }

    public static string BuildProcessRunNodeKey(Guid runId)
    {
        return $"{ProcessRunPrefix}{runId:D}";
    }

    public static bool TryParseProcessDefinitionNodeKey(string nodeKey, out Guid definitionId)
    {
        return TryParsePrefixedGuidNodeKey(nodeKey, ProcessDefinitionPrefix, out definitionId);
    }

    public static bool TryParseProcessRunNodeKey(string nodeKey, out Guid runId)
    {
        return TryParsePrefixedGuidNodeKey(nodeKey, ProcessRunPrefix, out runId);
    }

    public static bool TryParseProcessRunOutputNodeKey(string nodeKey, out Guid runId)
    {
        runId = Guid.Empty;
        if (!nodeKey.StartsWith(ProcessRunOutputPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remaining = nodeKey[ProcessRunOutputPrefix.Length..];
        var separatorIndex = remaining.IndexOf(':', StringComparison.Ordinal);
        var runIdText = separatorIndex < 0
            ? remaining
            : remaining[..separatorIndex];
        return Guid.TryParse(runIdText, out runId);
    }

    private static bool TryParsePrefixedGuidNodeKey(string nodeKey, string prefix, out Guid value)
    {
        if (nodeKey.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParse(nodeKey[prefix.Length..], out value))
        {
            return true;
        }

        value = Guid.Empty;
        return false;
    }
}
