using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class IncludeCognitiveMemoryModuleModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CognitiveMemory_* tables were created by the initial PostgreSQL baseline and retained by the retire migration.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep the retained CognitiveMemory_* tables intact; this migration only restores the EF model snapshot.
        }
    }
}
