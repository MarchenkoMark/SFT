#nullable disable

using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SnakeForTwo.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SnakeForTwoDbContext))]
[Migration("20260701150000_CreateMatchPersistence")]
public partial class CreateMatchPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "match_summaries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                match_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                room_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                duration_ticks = table.Column<long>(type: "bigint", nullable: false),
                duration_ms = table.Column<long>(type: "bigint", nullable: false),
                result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                player_count = table.Column<int>(type: "integer", nullable: false),
                board_width = table.Column<int>(type: "integer", nullable: false),
                board_height = table.Column<int>(type: "integer", nullable: false),
                seed = table.Column<int>(type: "integer", nullable: false),
                final_state_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_match_summaries", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "match_participants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                match_summary_id = table.Column<Guid>(type: "uuid", nullable: false),
                temporary_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                player_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                seat = table.Column<int>(type: "integer", nullable: false),
                alive = table.Column<bool>(type: "boolean", nullable: false),
                final_length = table.Column<int>(type: "integer", nullable: false),
                own_food_eaten = table.Column<int>(type: "integer", nullable: false),
                teammate_food_eaten = table.Column<int>(type: "integer", nullable: false),
                food_eaten_by_teammates = table.Column<int>(type: "integer", nullable: false),
                score = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_match_participants", x => x.id);
                table.ForeignKey(
                    name: "fk_match_participants_match_summaries_match_summary_id",
                    column: x => x.match_summary_id,
                    principalTable: "match_summaries",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_match_participants_match_summary_id_player_id",
            table: "match_participants",
            columns: new[] { "match_summary_id", "player_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_match_participants_score",
            table: "match_participants",
            column: "score");

        migrationBuilder.CreateIndex(
            name: "ix_match_participants_temporary_user_id",
            table: "match_participants",
            column: "temporary_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_match_summaries_finished_at",
            table: "match_summaries",
            column: "finished_at");

        migrationBuilder.CreateIndex(
            name: "ix_match_summaries_match_id",
            table: "match_summaries",
            column: "match_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "match_participants");
        migrationBuilder.DropTable(name: "match_summaries");
    }
}
