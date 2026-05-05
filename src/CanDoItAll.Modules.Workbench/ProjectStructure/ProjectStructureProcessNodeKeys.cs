namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessNodeKeys
{
    public const string ProcessDefinitionPrefix = "process-definition:";
    public const string ProcessRunPrefix = "process-run:";

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
