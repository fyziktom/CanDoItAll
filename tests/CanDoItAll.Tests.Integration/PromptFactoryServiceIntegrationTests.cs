using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PromptFactoryServiceIntegrationTests
{
    [Fact]
    public async Task GetEditorAsync_repairs_legacy_factory_schema_and_seeds_missing_templates()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var legacyBlockId = Guid.NewGuid();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptBuildSessions";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptRunNodes";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptRuns";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptBlueprints";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptFlowTemplates";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptBlocks";""");

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "Factory_PromptBlocks" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "BlockKind" INTEGER NOT NULL,
                    "Summary" TEXT NOT NULL DEFAULT '',
                    "Content" TEXT NOT NULL DEFAULT '',
                    "IsRecommendedByDefault" INTEGER NOT NULL DEFAULT 0
                );
                """);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Factory_PromptBlocks" ("Id", "Name", "BlockKind", "Summary", "Content", "IsRecommendedByDefault")
                VALUES ({legacyBlockId}, {"Legacy Block"}, {0}, {"Legacy summary"}, {"Legacy content"}, {1});
                """);
        }

        var editor = await factory.GetEditorAsync(null);
        var blocks = await factory.ListBlocksAsync();
        var templates = await factory.ListTemplatesAsync();
        var blueprints = await factory.ListBlueprintsAsync();

        Assert.NotNull(editor.FlowTemplateId);
        Assert.NotNull(editor.BlueprintId);
        Assert.Contains(blocks, item => item.Id == legacyBlockId && item.Name == "Legacy Block");
        Assert.NotEmpty(templates);
        Assert.NotEmpty(blueprints);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var blockColumns = await ReadColumnNamesAsync(verificationContext, "Factory_PromptBlocks");
        var templateColumns = await ReadColumnNamesAsync(verificationContext, "Factory_PromptFlowTemplates");

        Assert.Contains("PromptTypeRules", blockColumns);
        Assert.Contains("BlueprintRules", blockColumns);
        Assert.Contains("PhaseRules", blockColumns);
        Assert.Contains("PromptTypeRules", templateColumns);
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(AppDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""PRAGMA table_info("{tableName}");""";

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(1))
                {
                    columns.Add(reader.GetString(1));
                }
            }

            return columns;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
