using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260525140500_ProcessArtifactProjectionLineage")]
    public partial class ProcessArtifactProjectionLineage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectionLineageJson",
                table: "Processes_ArtifactRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectionLineageJson",
                table: "Processes_ArtifactRecords");
        }
    }
}
