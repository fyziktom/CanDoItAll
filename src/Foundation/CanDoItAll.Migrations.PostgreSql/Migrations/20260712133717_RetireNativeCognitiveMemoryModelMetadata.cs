using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations;

public partial class RetireNativeCognitiveMemoryModelMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Align EF metadata without destroying native cognitive-memory tables retained for safe migration/export.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
