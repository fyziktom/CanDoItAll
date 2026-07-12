using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260712133000_DistributedMemoryWorkerPhaseLeases")]
public sealed class DistributedMemoryWorkerPhaseLeases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Memory_WorkerLeases",
            columns: table => new
            {
                Phase = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OwnerId = table.Column<string>(
                    type: "character varying(180)",
                    maxLength: 180,
                    nullable: false),
                LeaseToken = table.Column<Guid>(type: "uuid", nullable: false),
                LeaseExpiresAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Memory_WorkerLeases", item => item.Phase);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Memory_WorkerLeases_LeaseExpiresAtUtc",
            table: "Memory_WorkerLeases",
            column: "LeaseExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Memory_WorkerLeases");
    }
}
