using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rfq.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UseIncrementalCaseId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSequence(
            name: "rfq_case_id_seq");

        migrationBuilder.Sql(
            """
            ALTER TABLE case_currents
                ADD COLUMN case_id_incremental bigint;
            ALTER TABLE rfq_revisions
                ADD COLUMN case_id_incremental bigint;
            ALTER TABLE rfq_cases
                ADD COLUMN case_id_incremental bigint;

            WITH case_id_mapping AS (
                SELECT
                    case_id,
                    row_number() OVER (ORDER BY created_at, case_id)::bigint AS new_case_id
                FROM rfq_cases
            )
            UPDATE rfq_cases AS cases
            SET case_id_incremental = mapping.new_case_id
            FROM case_id_mapping AS mapping
            WHERE cases.case_id = mapping.case_id;

            UPDATE case_currents AS currents
            SET case_id_incremental = cases.case_id_incremental
            FROM rfq_cases AS cases
            WHERE currents.case_id = cases.case_id;

            UPDATE rfq_revisions AS revision
            SET case_id_incremental = cases.case_id_incremental
            FROM rfq_cases AS cases
            WHERE revision.case_id = cases.case_id;

            ALTER TABLE case_currents
                DROP CONSTRAINT "FK_case_currents_rfq_cases_case_id";
            ALTER TABLE rfq_revisions
                DROP CONSTRAINT "FK_rfq_revisions_rfq_cases_case_id";
            DROP INDEX ux_rfq_revisions_one_draft_per_case;
            ALTER TABLE case_currents
                DROP CONSTRAINT "PK_case_currents";
            ALTER TABLE rfq_cases
                DROP CONSTRAINT "PK_rfq_cases";

            ALTER TABLE case_currents DROP COLUMN case_id;
            ALTER TABLE rfq_revisions DROP COLUMN case_id;
            ALTER TABLE rfq_cases DROP COLUMN case_id;

            ALTER TABLE case_currents
                RENAME COLUMN case_id_incremental TO case_id;
            ALTER TABLE rfq_revisions
                RENAME COLUMN case_id_incremental TO case_id;
            ALTER TABLE rfq_cases
                RENAME COLUMN case_id_incremental TO case_id;

            ALTER TABLE case_currents ALTER COLUMN case_id SET NOT NULL;
            ALTER TABLE rfq_revisions ALTER COLUMN case_id SET NOT NULL;
            ALTER TABLE rfq_cases ALTER COLUMN case_id SET NOT NULL;

            ALTER TABLE rfq_cases
                ADD CONSTRAINT "PK_rfq_cases" PRIMARY KEY (case_id);
            ALTER TABLE case_currents
                ADD CONSTRAINT "PK_case_currents" PRIMARY KEY (case_id);
            ALTER TABLE case_currents
                ADD CONSTRAINT "FK_case_currents_rfq_cases_case_id"
                FOREIGN KEY (case_id) REFERENCES rfq_cases (case_id) ON DELETE CASCADE;
            ALTER TABLE rfq_revisions
                ADD CONSTRAINT "FK_rfq_revisions_rfq_cases_case_id"
                FOREIGN KEY (case_id) REFERENCES rfq_cases (case_id) ON DELETE CASCADE;
            CREATE UNIQUE INDEX ux_rfq_revisions_one_draft_per_case
                ON rfq_revisions (case_id)
                WHERE status = 'Draft';

            SELECT setval(
                'rfq_case_id_seq',
                COALESCE((SELECT MAX(case_id) FROM rfq_cases), 0) + 1,
                false);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE case_currents ADD COLUMN case_id_uuid uuid;
            ALTER TABLE rfq_revisions ADD COLUMN case_id_uuid uuid;
            ALTER TABLE rfq_cases ADD COLUMN case_id_uuid uuid;

            UPDATE rfq_cases SET case_id_uuid = gen_random_uuid();

            UPDATE case_currents AS currents
            SET case_id_uuid = cases.case_id_uuid
            FROM rfq_cases AS cases
            WHERE currents.case_id = cases.case_id;

            UPDATE rfq_revisions AS revision
            SET case_id_uuid = cases.case_id_uuid
            FROM rfq_cases AS cases
            WHERE revision.case_id = cases.case_id;

            ALTER TABLE case_currents
                DROP CONSTRAINT "FK_case_currents_rfq_cases_case_id";
            ALTER TABLE rfq_revisions
                DROP CONSTRAINT "FK_rfq_revisions_rfq_cases_case_id";
            DROP INDEX ux_rfq_revisions_one_draft_per_case;
            ALTER TABLE case_currents
                DROP CONSTRAINT "PK_case_currents";
            ALTER TABLE rfq_cases
                DROP CONSTRAINT "PK_rfq_cases";

            ALTER TABLE case_currents DROP COLUMN case_id;
            ALTER TABLE rfq_revisions DROP COLUMN case_id;
            ALTER TABLE rfq_cases DROP COLUMN case_id;

            ALTER TABLE case_currents RENAME COLUMN case_id_uuid TO case_id;
            ALTER TABLE rfq_revisions RENAME COLUMN case_id_uuid TO case_id;
            ALTER TABLE rfq_cases RENAME COLUMN case_id_uuid TO case_id;

            ALTER TABLE case_currents ALTER COLUMN case_id SET NOT NULL;
            ALTER TABLE rfq_revisions ALTER COLUMN case_id SET NOT NULL;
            ALTER TABLE rfq_cases ALTER COLUMN case_id SET NOT NULL;

            ALTER TABLE rfq_cases
                ADD CONSTRAINT "PK_rfq_cases" PRIMARY KEY (case_id);
            ALTER TABLE case_currents
                ADD CONSTRAINT "PK_case_currents" PRIMARY KEY (case_id);
            ALTER TABLE case_currents
                ADD CONSTRAINT "FK_case_currents_rfq_cases_case_id"
                FOREIGN KEY (case_id) REFERENCES rfq_cases (case_id) ON DELETE CASCADE;
            ALTER TABLE rfq_revisions
                ADD CONSTRAINT "FK_rfq_revisions_rfq_cases_case_id"
                FOREIGN KEY (case_id) REFERENCES rfq_cases (case_id) ON DELETE CASCADE;
            CREATE UNIQUE INDEX ux_rfq_revisions_one_draft_per_case
                ON rfq_revisions (case_id)
                WHERE status = 'Draft';
            """);

        migrationBuilder.DropSequence(
            name: "rfq_case_id_seq");
    }
}
