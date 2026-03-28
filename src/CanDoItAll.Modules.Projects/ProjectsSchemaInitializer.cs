using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public static class ProjectsSchemaInitializer
{
    private static readonly string[] SqliteStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Projects_ProjectHierarchyLinks" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_ProjectHierarchyLinks" PRIMARY KEY,
            "ParentProjectId" TEXT NOT NULL,
            "ChildProjectId" TEXT NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Projects_ProjectHierarchyLinks_ParentProjectId_ChildProjectId"
        ON "Projects_ProjectHierarchyLinks" ("ParentProjectId", "ChildProjectId");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Projects_ProjectHierarchyLinks_ParentProjectId"
        ON "Projects_ProjectHierarchyLinks" ("ParentProjectId");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_Projects_ProjectHierarchyLinks_ChildProjectId"
        ON "Projects_ProjectHierarchyLinks" ("ChildProjectId");
        """
    ];

    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        foreach (var statement in SqliteStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
