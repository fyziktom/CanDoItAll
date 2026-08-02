using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RuntimeStorageToolNames
{
    public static readonly RuntimeToolName CatalogList = RuntimeToolName.Create(ToolContractCatalog.StorageCatalogList);
    public static readonly RuntimeToolName Browse = RuntimeToolName.Create(ToolContractCatalog.StorageBrowse);
    public static readonly RuntimeToolName ReadTextFile = RuntimeToolName.Create(ToolContractCatalog.StorageReadTextFile);
    public static readonly RuntimeToolName WriteTextFile = RuntimeToolName.Create(ToolContractCatalog.StorageWriteTextFile);
    public static readonly RuntimeToolName DeleteObject = RuntimeToolName.Create(ToolContractCatalog.StorageDeleteObject);

    public static IReadOnlyList<RuntimeToolName> All { get; } =
    [
        CatalogList,
        Browse,
        ReadTextFile,
        WriteTextFile,
        DeleteObject
    ];
}
