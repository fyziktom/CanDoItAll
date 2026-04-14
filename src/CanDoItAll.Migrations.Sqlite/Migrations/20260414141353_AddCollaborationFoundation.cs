using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Collaboration_InboxItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    PreviewText = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    Route = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsUnread = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnreadCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_InboxItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaboration_Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AuthorKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AuthorKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    RaisesEscalation = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaboration_Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ParticipantKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    RoleLabel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaboration_Threads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    ContextKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ContextId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContextLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContextRoute = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrimaryItemKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_Threads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_InboxItems_ItemKind_IsUnread",
                table: "Collaboration_InboxItems",
                columns: new[] { "ItemKind", "IsUnread" });

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_InboxItems_ThreadId",
                table: "Collaboration_InboxItems",
                column: "ThreadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_InboxItems_UpdatedAtUtc",
                table: "Collaboration_InboxItems",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Messages_ThreadId_CreatedAtUtc",
                table: "Collaboration_Messages",
                columns: new[] { "ThreadId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Participants_ThreadId_ParticipantKey",
                table: "Collaboration_Participants",
                columns: new[] { "ThreadId", "ParticipantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Threads_ContextKind_ContextId",
                table: "Collaboration_Threads",
                columns: new[] { "ContextKind", "ContextId" });

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Threads_LastActivityAtUtc",
                table: "Collaboration_Threads",
                column: "LastActivityAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Threads_ProjectId",
                table: "Collaboration_Threads",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Collaboration_InboxItems");

            migrationBuilder.DropTable(
                name: "Collaboration_Messages");

            migrationBuilder.DropTable(
                name: "Collaboration_Participants");

            migrationBuilder.DropTable(
                name: "Collaboration_Threads");
        }
    }
}
