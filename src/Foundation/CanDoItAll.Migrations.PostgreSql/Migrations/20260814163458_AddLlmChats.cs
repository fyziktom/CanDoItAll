using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmChats : Migration
    {
    protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmChats_Definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AvatarImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentRevision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_Definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmChats_Transcripts",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TranscriptRevision = table.Column<long>(type: "bigint", nullable: false),
                    EntryCount = table.Column<int>(type: "integer", nullable: false),
                    ActiveTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    PendingUserEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    TurnAdmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TurnAdmittedRevision = table.Column<long>(type: "bigint", nullable: true),
                    CompensationProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompensationProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompensationProviderKind = table.Column<int>(type: "integer", nullable: true),
                    CompensationModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompensationAccelerationStrategyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompensationAccelerationProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompensationAccelerationModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompensationAccelerationPayloadJson = table.Column<string>(type: "text", nullable: true),
                    AccelerationStrategyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccelerationProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccelerationModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccelerationPayloadJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_Transcripts", x => x.ConversationId);
                });

            migrationBuilder.CreateTable(
                name: "LlmChats_DefinitionRevisions",
                columns: table => new
                {
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AvatarImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SystemPrompt = table.Column<string>(type: "character varying(400000)", maxLength: 400000, nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    ThinkingEffort = table.Column<int>(type: "integer", nullable: true),
                    ModelParameterConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    TimeoutTicks = table.Column<long>(type: "bigint", nullable: true),
                    HasResponseFormat = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseRequireJson = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseSchemaJson = table.Column<string>(type: "text", nullable: false),
                    ResponseSchemaName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResponseSchemaDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SettingsFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_DefinitionRevisions", x => new { x.DefinitionId, x.Revision });
                    table.ForeignKey(
                        name: "FK_LlmChats_DefinitionRevisions_LlmChats_Definitions_Definitio~",
                        column: x => x.DefinitionId,
                        principalTable: "LlmChats_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LlmChats_DefinitionTags",
                columns: table => new
                {
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_DefinitionTags", x => new { x.DefinitionId, x.Tag });
                    table.ForeignKey(
                        name: "FK_LlmChats_DefinitionTags_LlmChats_Definitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "LlmChats_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LlmChats_Messages",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    TurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(400000)", maxLength: 400000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CachedInputTokens = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_Messages", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_LlmChats_Messages_LlmChats_Transcripts_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "LlmChats_Transcripts",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LlmChats_Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionRevision = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LlmChats_Conversations_LlmChats_DefinitionRevisions_Definit~",
                        columns: x => new { x.DefinitionId, x.DefinitionRevision },
                        principalTable: "LlmChats_DefinitionRevisions",
                        principalColumns: new[] { "DefinitionId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LlmChats_Conversations_LlmChats_Transcripts_Id",
                        column: x => x.Id,
                        principalTable: "LlmChats_Transcripts",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LlmChats_Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpectedTranscriptRevision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancellationRequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TurnAdmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderDispatchStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderDispatchReturnedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TranscriptCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultingTranscriptRevision = table.Column<long>(type: "bigint", nullable: true),
                    AssistantEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConcurrencyToken = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_Operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LlmChats_Operations_LlmChats_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "LlmChats_Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LlmChats_InvocationRecords",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedThinkingEffort = table.Column<int>(type: "integer", nullable: true),
                    EffectiveThinkingEffort = table.Column<int>(type: "integer", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CachedInputTokens = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_InvocationRecords", x => new { x.OperationId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LlmChats_InvocationRecords_LlmChats_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "LlmChats_Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Conversations_DefinitionId_DefinitionRevision",
                table: "LlmChats_Conversations",
                columns: new[] { "DefinitionId", "DefinitionRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Conversations_DefinitionId_UpdatedAtUtc",
                table: "LlmChats_Conversations",
                columns: new[] { "DefinitionId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Conversations_Status_UpdatedAtUtc",
                table: "LlmChats_Conversations",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_DefinitionRevisions_ProviderProfileId",
                table: "LlmChats_DefinitionRevisions",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Definitions_Status_Name",
                table: "LlmChats_Definitions",
                columns: new[] { "Status", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_DefinitionTags_Tag",
                table: "LlmChats_DefinitionTags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_InvocationRecords_ProviderProfileId_StartedAtUtc",
                table: "LlmChats_InvocationRecords",
                columns: new[] { "ProviderProfileId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Messages_ConversationId_Sequence",
                table: "LlmChats_Messages",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Messages_ConversationId_TurnId_Role",
                table: "LlmChats_Messages",
                columns: new[] { "ConversationId", "TurnId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Operations_ConversationId_StartedAtUtc",
                table: "LlmChats_Operations",
                columns: new[] { "ConversationId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Operations_Status_StartedAtUtc",
                table: "LlmChats_Operations",
                columns: new[] { "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Transcripts_UpdatedAtUtc",
                table: "LlmChats_Transcripts",
                column: "UpdatedAtUtc");
        }

    protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmChats_DefinitionTags");

            migrationBuilder.DropTable(
                name: "LlmChats_InvocationRecords");

            migrationBuilder.DropTable(
                name: "LlmChats_Messages");

            migrationBuilder.DropTable(
                name: "LlmChats_Operations");

            migrationBuilder.DropTable(
                name: "LlmChats_Conversations");

            migrationBuilder.DropTable(
                name: "LlmChats_DefinitionRevisions");

            migrationBuilder.DropTable(
                name: "LlmChats_Transcripts");

            migrationBuilder.DropTable(
                name: "LlmChats_Definitions");
        }
    }
}
