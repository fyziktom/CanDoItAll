using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class WorkspaceSpreadsheetRuntimeToolFactory
{
    private static readonly JsonNode ScalarCellValueSchema = JsonNode.Parse(
        """
        {
          "type": ["string", "number", "boolean", "null"]
        }
        """)!;

    public static AIFunction CreateWriteTool(
        WorkspaceSpreadsheetRuntimePlugin plugin,
        string name,
        string description)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        return AIFunctionFactory.Create(
            plugin.WriteSpreadsheetWorkbookFromToolArguments,
            new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
                JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions
                {
                    TransformSchemaNode = (context, schema) =>
                        context.TypeInfo.Type == typeof(JsonElement)
                            ? ScalarCellValueSchema.DeepClone()
                            : schema
                }
            });
    }
}
