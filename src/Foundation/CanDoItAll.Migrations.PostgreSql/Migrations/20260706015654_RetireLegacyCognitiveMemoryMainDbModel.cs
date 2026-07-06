using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class RetireLegacyCognitiveMemoryMainDbModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy CognitiveMemory_* tables are retained read-only for export/native-service import.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-adding retired model metadata requires restoring the legacy module and a new migration.
        }
    }
}
