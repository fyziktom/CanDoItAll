using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageRootHostBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RootBindingFormatVersion",
                table: "Storage_Catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RootHostBindingId",
                table: "Storage_Catalog",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RootLastValidatedAtUtc",
                table: "Storage_Catalog",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RootPathState",
                table: "Storage_Catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RootPathSyntax",
                table: "Storage_Catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RootPlatformFamily",
                table: "Storage_Catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RootBindingFormatVersion",
                table: "Storage_Catalog");

            migrationBuilder.DropColumn(
                name: "RootHostBindingId",
                table: "Storage_Catalog");

            migrationBuilder.DropColumn(
                name: "RootLastValidatedAtUtc",
                table: "Storage_Catalog");

            migrationBuilder.DropColumn(
                name: "RootPathState",
                table: "Storage_Catalog");

            migrationBuilder.DropColumn(
                name: "RootPathSyntax",
                table: "Storage_Catalog");

            migrationBuilder.DropColumn(
                name: "RootPlatformFamily",
                table: "Storage_Catalog");
        }
    }
}
