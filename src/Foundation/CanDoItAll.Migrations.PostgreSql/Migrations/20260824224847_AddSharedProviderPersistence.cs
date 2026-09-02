using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedProviderPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workspace_ProviderSharePublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProviderSharePublications", x => x.Id);
                    table.UniqueConstraint("AK_Workspace_ProviderSharePublications_PublicId", x => x.PublicId);
                    table.UniqueConstraint("AK_Workspace_ProviderSharePublications_PublicId_ProviderProfil~", x => new { x.PublicId, x.ProviderProfileId });
                    table.CheckConstraint("CK_Workspace_ProviderSharePublications_PublicIdentity", "\"PublicId\" <> \"ProviderProfileId\"");
                    table.ForeignKey(
                        name: "FK_Workspace_ProviderSharePublications_Workspace_ProviderProfi~",
                        column: x => x.ProviderProfileId,
                        principalTable: "Workspace_ProviderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_SharedProviderServiceIdentity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_SharedProviderServiceIdentity", x => x.Id);
                    table.CheckConstraint("CK_Workspace_SharedProviderServiceIdentity_Singleton", "\"Id\" = '7d5f45ad-9b13-4f1a-9284-260e2e07c92c'");
                });

            migrationBuilder.CreateTable(
                name: "Workspace_SharedProviderSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ApiTokenSecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AllowInsecurePrivateNetwork = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RemoteInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCatalogETag = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: true),
                    LastSyncAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStatusCode = table.Column<int>(type: "integer", nullable: true),
                    LastStatusMessage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_SharedProviderSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspace_SharedProviderSources_Security_SecretRecords_ApiT~",
                        column: x => x.ApiTokenSecretId,
                        principalTable: "Security_SecretRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_SharedProviderInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthenticatedSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccessContextReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicModelId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UpstreamModelId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InputTokenCount = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokenCount = table.Column<long>(type: "bigint", nullable: true),
                    ImageCount = table.Column<int>(type: "integer", nullable: true),
                    UsageCompleteness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    PricingCompleteness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeleteAfterUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_SharedProviderInvocations", x => x.Id);
                    table.CheckConstraint("CK_Workspace_SharedProviderInvocations_Completion", "(\"Outcome\" = 'InProgress' AND \"CompletedAtUtc\" IS NULL AND \"DurationMilliseconds\" IS NULL) OR (\"Outcome\" <> 'InProgress' AND \"CompletedAtUtc\" IS NOT NULL AND \"DurationMilliseconds\" IS NOT NULL)");
                    table.CheckConstraint("CK_Workspace_SharedProviderInvocations_Usage", "(\"InputTokenCount\" IS NULL OR \"InputTokenCount\" >= 0) AND (\"OutputTokenCount\" IS NULL OR \"OutputTokenCount\" >= 0) AND (\"ImageCount\" IS NULL OR \"ImageCount\" BETWEEN 1 AND 16) AND ((\"UsageCompleteness\" = 'Unavailable' AND \"InputTokenCount\" IS NULL AND \"OutputTokenCount\" IS NULL AND \"ImageCount\" IS NULL AND \"Operation\" IN ('ChatCompletions', 'Responses', 'ImageGenerations')) OR (\"Operation\" IN ('ChatCompletions', 'Responses') AND \"ImageCount\" IS NULL AND ((\"UsageCompleteness\" = 'Partial' AND ((\"InputTokenCount\" IS NOT NULL AND \"OutputTokenCount\" IS NULL) OR (\"InputTokenCount\" IS NULL AND \"OutputTokenCount\" IS NOT NULL))) OR (\"UsageCompleteness\" = 'Complete' AND \"InputTokenCount\" IS NOT NULL AND \"OutputTokenCount\" IS NOT NULL))) OR (\"Operation\" = 'ImageGenerations' AND \"UsageCompleteness\" = 'Complete' AND \"InputTokenCount\" IS NULL AND \"OutputTokenCount\" IS NULL AND \"ImageCount\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_Workspace_SharedProviderInvocations_Workspace_ProviderShare~",
                        columns: x => new { x.PublicationId, x.ProviderProfileId },
                        principalTable: "Workspace_ProviderSharePublications",
                        principalColumns: new[] { "PublicId", "ProviderProfileId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_SharedProviderImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemotePublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemoteDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RemoteRevision = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    RemotePurpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RemoteTransport = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RemoteDefaultModelId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RemoteCatalogSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    SelectionState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AvailabilityState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_SharedProviderImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspace_SharedProviderImports_Workspace_ProviderProfiles_~",
                        column: x => x.ProviderProfileId,
                        principalTable: "Workspace_ProviderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Workspace_SharedProviderImports_Workspace_SharedProviderSou~",
                        column: x => x.SourceId,
                        principalTable: "Workspace_SharedProviderSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ProviderSharePublications_IsPublished_UpdatedAtUtc",
                table: "Workspace_ProviderSharePublications",
                columns: new[] { "IsPublished", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ProviderSharePublications_ProviderProfileId",
                table: "Workspace_ProviderSharePublications",
                column: "ProviderProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderImports_ProviderProfileId",
                table: "Workspace_SharedProviderImports",
                column: "ProviderProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderImports_SelectionState_Availability~",
                table: "Workspace_SharedProviderImports",
                columns: new[] { "SelectionState", "AvailabilityState", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderImports_SourceId_RemotePublicationId",
                table: "Workspace_SharedProviderImports",
                columns: new[] { "SourceId", "RemotePublicationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderInvocations_DeleteAfterUtc_Complete~",
                table: "Workspace_SharedProviderInvocations",
                columns: new[] { "DeleteAfterUtc", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderInvocations_PublicationId_ProviderP~",
                table: "Workspace_SharedProviderInvocations",
                columns: new[] { "PublicationId", "ProviderProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderInvocations_PublicationId_StartedAt~",
                table: "Workspace_SharedProviderInvocations",
                columns: new[] { "PublicationId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderInvocations_RequestId",
                table: "Workspace_SharedProviderInvocations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderServiceIdentity_PublicId",
                table: "Workspace_SharedProviderServiceIdentity",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderSources_ApiTokenSecretId",
                table: "Workspace_SharedProviderSources",
                column: "ApiTokenSecretId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderSources_BaseUri",
                table: "Workspace_SharedProviderSources",
                column: "BaseUri");

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_SharedProviderSources_IsEnabled_Status_UpdatedAtU~",
                table: "Workspace_SharedProviderSources",
                columns: new[] { "IsEnabled", "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workspace_SharedProviderImports");

            migrationBuilder.DropTable(
                name: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropTable(
                name: "Workspace_SharedProviderServiceIdentity");

            migrationBuilder.DropTable(
                name: "Workspace_SharedProviderSources");

            migrationBuilder.DropTable(
                name: "Workspace_ProviderSharePublications");
        }
    }
}
