using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginOAuthConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plugins_OAuthConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    TokenVaultKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AccountDisplay = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    GrantedScopesJson = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefreshTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LastErrorDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_OAuthConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_OAuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CodeVerifierVaultKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReturnPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedScopesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ErrorDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_OAuthSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthConnections_ConnectionId",
                table: "Plugins_OAuthConnections",
                column: "ConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthConnections_PluginId_ConnectionKey",
                table: "Plugins_OAuthConnections",
                columns: new[] { "PluginId", "ConnectionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthSessions_PluginId_ConnectionId_Status",
                table: "Plugins_OAuthSessions",
                columns: new[] { "PluginId", "ConnectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthSessions_StateHash",
                table: "Plugins_OAuthSessions",
                column: "StateHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plugins_OAuthConnections");

            migrationBuilder.DropTable(
                name: "Plugins_OAuthSessions");
        }
    }
}
