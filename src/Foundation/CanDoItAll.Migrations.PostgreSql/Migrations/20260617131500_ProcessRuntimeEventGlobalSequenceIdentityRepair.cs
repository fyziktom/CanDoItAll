using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260617131500_ProcessRuntimeEventGlobalSequenceIdentityRepair")]
    public partial class ProcessRuntimeEventGlobalSequenceIdentityRepair : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    sequence_name text;
                BEGIN
                    SELECT pg_get_serial_sequence('public.process_runtime_events', 'GlobalSequence')
                    INTO sequence_name;

                    IF sequence_name IS NOT NULL THEN
                        EXECUTE format(
                            'SELECT setval(%L, COALESCE((SELECT MAX("GlobalSequence") FROM public.process_runtime_events), 0) + 1, false)',
                            sequence_name);
                    END IF;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
