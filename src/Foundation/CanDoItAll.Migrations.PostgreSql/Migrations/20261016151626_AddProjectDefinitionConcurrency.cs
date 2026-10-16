using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProjectDefinitionConcurrency : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "Projects_Projects",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Projects_Projects");
        }
    }
}
