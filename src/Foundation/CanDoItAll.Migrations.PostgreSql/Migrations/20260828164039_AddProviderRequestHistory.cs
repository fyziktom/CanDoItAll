using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProviderRequestHistory : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "ProviderHistory_Partitions",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecurityPartition = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Partitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_HostLeases",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_HostLeases", x => x.Id);
                    table.UniqueConstraint("AK_ProviderHistory_HostLeases_Id_PartitionId", x => new { x.Id, x.PartitionId });
                    table.ForeignKey(
                        name: "FK_ProviderHistory_HostLeases_ProviderHistory_Partitions_Parti~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_Checkpoints",
                columns: table => new {
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    Coverage = table.Column<int>(type: "integer", nullable: false),
                    Cursor = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    IndexedThroughUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Checkpoints", x => new { x.PartitionId, x.SourceKind });
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Checkpoints_ProviderHistory_Partitions_Part~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_Outbox",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Mutation = table.Column<string>(type: "jsonb", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    RetryAfterUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Outbox_ProviderHistory_Partitions_Partition~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_Policies",
                columns: table => new {
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureMode = table.Column<int>(type: "integer", nullable: false),
                    MetadataRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    DetailRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MaximumTextBytes = table.Column<int>(type: "integer", nullable: false),
                    DetailQuotaBytes = table.Column<long>(type: "bigint", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    UsedDetailBytes = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Policies", x => x.PartitionId);
                    table.CheckConstraint("CK_ProviderHistory_Quota", "\"UsedDetailBytes\" >= 0 AND \"DetailQuotaBytes\" > 0 AND \"MetadataRetentionDays\" BETWEEN 1 AND 3650 AND \"DetailRetentionDays\" BETWEEN 1 AND \"MetadataRetentionDays\" AND \"MaximumTextBytes\" BETWEEN 1 AND 131072 AND \"BatchSize\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Policies_ProviderHistory_Partitions_Partiti~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_PolicyAudit",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Policy = table.Column<string>(type: "jsonb", nullable: false),
                    AppliedShorterRetention = table.Column<bool>(type: "boolean", nullable: false),
                    Caller = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_PolicyAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderHistory_PolicyAudit_ProviderHistory_Partitions_Part~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_Sources",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EvidenceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MutationHash = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Sources", x => x.Id);
                    table.UniqueConstraint("AK_ProviderHistory_Sources_Id_PartitionId", x => new { x.Id, x.PartitionId });
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Sources_ProviderHistory_Partitions_Partitio~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_StorageIdentity",
                columns: table => new {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_StorageIdentity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderHistory_StorageIdentity_ProviderHistory_Partitions_~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_Details",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputRevision = table.Column<long>(type: "bigint", nullable: false),
                    Part = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ProtectedText = table.Column<string>(type: "text", nullable: false),
                    StoredBytes = table.Column<int>(type: "integer", nullable: false),
                    CapturedBytes = table.Column<int>(type: "integer", nullable: false),
                    OriginalBytes = table.Column<long>(type: "bigint", nullable: false),
                    Flags = table.Column<int>(type: "integer", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Details", x => x.Id);
                    table.UniqueConstraint("AK_ProviderHistory_Details_Id_PartitionId", x => new { x.Id, x.PartitionId });
                    table.CheckConstraint("CK_ProviderHistory_DetailPart", "(\"Part\" = 0 AND \"EntryId\" IS NULL) OR (\"Part\" = 1 AND \"EntryId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Details_ProviderHistory_Partitions_Partitio~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_Entries",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaptureHostId = table.Column<Guid>(type: "uuid", nullable: true),
                    Granularity = table.Column<int>(type: "integer", nullable: false),
                    SortAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeBasis = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderKind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestedModel = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ResolvedModel = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    Workload = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    AuthenticationKind = table.Column<int>(type: "integer", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    Issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Subject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CallerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UsageState = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CachedInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CacheWriteTokens = table.Column<long>(type: "bigint", nullable: true),
                    ReasoningTokens = table.Column<long>(type: "bigint", nullable: true),
                    ImageCount = table.Column<int>(type: "integer", nullable: true),
                    PriceState = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PriceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PriceVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetadataAuthority = table.Column<int>(type: "integer", nullable: false),
                    RetentionAuthority = table.Column<int>(type: "integer", nullable: false),
                    DetailState = table.Column<int>(type: "integer", nullable: false),
                    InputDetailId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemoteSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RemoteRequestId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Entries", x => x.Id);
                    table.UniqueConstraint("AK_ProviderHistory_Entries_Id_PartitionId", x => new { x.Id, x.PartitionId });
                    table.CheckConstraint("CK_ProviderHistory_Granularity", "(\"Granularity\" = 0 AND \"AttemptId\" IS NOT NULL AND \"StartedAtUtc\" IS NOT NULL) OR \"Granularity\" = 1");
                    table.CheckConstraint("CK_ProviderHistory_Tokens", "(\"InputTokens\" IS NULL OR \"InputTokens\" >= 0) AND (\"OutputTokens\" IS NULL OR \"OutputTokens\" >= 0) AND (\"Amount\" IS NULL OR \"Amount\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Entries_ProviderHistory_Details_InputDetail~",
                        columns: x => new { x.InputDetailId, x.PartitionId },
                        principalTable: "ProviderHistory_Details",
                        principalColumns: new[] { "Id", "PartitionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Entries_ProviderHistory_HostLeases_CaptureH~",
                        columns: x => new { x.CaptureHostId, x.PartitionId },
                        principalTable: "ProviderHistory_HostLeases",
                        principalColumns: new[] { "Id", "PartitionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Entries_ProviderHistory_Partitions_Partitio~",
                        column: x => x.PartitionId,
                        principalTable: "ProviderHistory_Partitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderHistory_Owners",
                columns: table => new {
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ProviderHistory_Owners", x => new { x.SourceId, x.EntryId });
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Owners_ProviderHistory_Entries_EntryId_Part~",
                        columns: x => new { x.EntryId, x.PartitionId },
                        principalTable: "ProviderHistory_Entries",
                        principalColumns: new[] { "Id", "PartitionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProviderHistory_Owners_ProviderHistory_Sources_SourceId_Par~",
                        columns: x => new { x.SourceId, x.PartitionId },
                        principalTable: "ProviderHistory_Sources",
                        principalColumns: new[] { "Id", "PartitionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Details_EntryId_PartitionId",
                table: "ProviderHistory_Details",
                columns: new[] { "EntryId", "PartitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Details_PartitionId_EntryId_Part",
                table: "ProviderHistory_Details",
                columns: new[] { "PartitionId", "EntryId", "Part" },
                unique: true,
                filter: "\"Part\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Details_PartitionId_ExpiresAtUtc_Id",
                table: "ProviderHistory_Details",
                columns: new[] { "PartitionId", "ExpiresAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Details_PartitionId_RequestId_InputRevision~",
                table: "ProviderHistory_Details",
                columns: new[] { "PartitionId", "RequestId", "InputRevision", "Part" },
                unique: true,
                filter: "\"Part\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_CaptureHostId_PartitionId",
                table: "ProviderHistory_Entries",
                columns: new[] { "CaptureHostId", "PartitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_InputDetailId_PartitionId",
                table: "ProviderHistory_Entries",
                columns: new[] { "InputDetailId", "PartitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_PartitionId_AttemptId",
                table: "ProviderHistory_Entries",
                columns: new[] { "PartitionId", "AttemptId" },
                unique: true,
                filter: "\"AttemptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_PartitionId_CredentialId_SortAtUtc_~",
                table: "ProviderHistory_Entries",
                columns: new[] { "PartitionId", "CredentialId", "SortAtUtc", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_PartitionId_Outcome_ExpiresAtUtc",
                table: "ProviderHistory_Entries",
                columns: new[] { "PartitionId", "Outcome", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_PartitionId_ProviderId_SortAtUtc_Id",
                table: "ProviderHistory_Entries",
                columns: new[] { "PartitionId", "ProviderId", "SortAtUtc", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_PartitionId_SortAtUtc_Id",
                table: "ProviderHistory_Entries",
                columns: new[] { "PartitionId", "SortAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_HostLeases_PartitionId_ExpiresAtUtc",
                table: "ProviderHistory_HostLeases",
                columns: new[] { "PartitionId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Outbox_PartitionId_RetryAfterUtc_CreatedAtU~",
                table: "ProviderHistory_Outbox",
                columns: new[] { "PartitionId", "RetryAfterUtc", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Owners_EntryId_PartitionId",
                table: "ProviderHistory_Owners",
                columns: new[] { "EntryId", "PartitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Owners_SourceId_PartitionId",
                table: "ProviderHistory_Owners",
                columns: new[] { "SourceId", "PartitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_PolicyAudit_PartitionId_Version",
                table: "ProviderHistory_PolicyAudit",
                columns: new[] { "PartitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Sources_PartitionId_Kind_OwnerId_EvidenceId",
                table: "ProviderHistory_Sources",
                columns: new[] { "PartitionId", "Kind", "OwnerId", "EvidenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_StorageIdentity_PartitionId",
                table: "ProviderHistory_StorageIdentity",
                column: "PartitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderHistory_Details_ProviderHistory_Entries_EntryId_Par~",
                table: "ProviderHistory_Details",
                columns: new[] { "EntryId", "PartitionId" },
                principalTable: "ProviderHistory_Entries",
                principalColumns: new[] { "Id", "PartitionId" },
                onDelete: ReferentialAction.Restrict);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
                name: "FK_ProviderHistory_Details_ProviderHistory_Entries_EntryId_Par~",
                table: "ProviderHistory_Details");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Checkpoints");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Outbox");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Owners");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Policies");

            migrationBuilder.DropTable(
                name: "ProviderHistory_PolicyAudit");

            migrationBuilder.DropTable(
                name: "ProviderHistory_StorageIdentity");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Sources");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Entries");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Details");

            migrationBuilder.DropTable(
                name: "ProviderHistory_HostLeases");

            migrationBuilder.DropTable(
                name: "ProviderHistory_Partitions");
        }
    }
}
