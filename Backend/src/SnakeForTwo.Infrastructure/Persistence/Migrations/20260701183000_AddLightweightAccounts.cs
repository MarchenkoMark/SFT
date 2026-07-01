#nullable disable

using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SnakeForTwo.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SnakeForTwoDbContext))]
[Migration("20260701183000_AddLightweightAccounts")]
public partial class AddLightweightAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                username = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                normalized_username = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                picture_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                has_custom_username = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_signed_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_accounts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_logins",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                provider_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                picture_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_signed_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_logins", x => x.id);
                table.ForeignKey(
                    name: "fk_user_logins_user_accounts_user_id",
                    column: x => x.user_id,
                    principalTable: "user_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "user_id",
            table: "match_participants",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_match_participants_user_id",
            table: "match_participants",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_user_accounts_normalized_email",
            table: "user_accounts",
            column: "normalized_email");

        migrationBuilder.CreateIndex(
            name: "ix_user_accounts_normalized_username",
            table: "user_accounts",
            column: "normalized_username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_user_logins_provider_provider_user_id",
            table: "user_logins",
            columns: new[] { "provider", "provider_user_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_user_logins_user_id",
            table: "user_logins",
            column: "user_id");

        migrationBuilder.AddForeignKey(
            name: "fk_match_participants_user_accounts_user_id",
            table: "match_participants",
            column: "user_id",
            principalTable: "user_accounts",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_match_participants_user_accounts_user_id",
            table: "match_participants");

        migrationBuilder.DropTable(name: "user_logins");

        migrationBuilder.DropTable(name: "user_accounts");

        migrationBuilder.DropIndex(
            name: "ix_match_participants_user_id",
            table: "match_participants");

        migrationBuilder.DropColumn(
            name: "user_id",
            table: "match_participants");
    }
}
