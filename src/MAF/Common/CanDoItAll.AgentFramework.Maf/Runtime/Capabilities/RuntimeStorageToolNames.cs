using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RuntimeStorageToolNames
{
    public static readonly RuntimeToolName CatalogList = RuntimeToolName.Create("storage_catalog_list");
    public static readonly RuntimeToolName ReadTextFile = RuntimeToolName.Create("storage_read_text_file");
    public static readonly RuntimeToolName WriteTextFile = RuntimeToolName.Create("storage_write_text_file");
    public static readonly RuntimeToolName DeleteObject = RuntimeToolName.Create("storage_delete_object");

    public static IReadOnlyList<RuntimeToolName> All { get; } =
    [
        CatalogList,
        ReadTextFile,
        WriteTextFile,
        DeleteObject
    ];
}
