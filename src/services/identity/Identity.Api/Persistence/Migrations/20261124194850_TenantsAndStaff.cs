using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantsAndStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_role",
                table: "role_assignments");

            migrationBuilder.DropIndex(
                name: "IX_identity_users_NormalizedEmail",
                table: "identity_users");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "identity_users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                table: "identity_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_role_assignments",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_role_assignments", x => new { x.UserId, x.Role });
                    table.CheckConstraint("ck_platform_role_assignments_role", "\"Role\" IN ('identity.platform-administrator')");
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                    table.CheckConstraint("ck_tenants_slug", "\"Slug\" ~ '^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$'");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_role",
                table: "role_assignments",
                sql: "\"Role\" IN ('identity.administrator', 'identity.viewer')");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_staff_email",
                table: "identity_users",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"UserType\" <> 'customer'");

            migrationBuilder.CreateIndex(
                name: "ux_tenants_slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.Sql("UPDATE identity_users SET \"CreatedAt\" = now();");

            migrationBuilder.Sql(
                "INSERT INTO platform_role_assignments (\"UserId\", \"Role\", \"AssignedAt\") " +
                "SELECT \"Id\", 'identity.platform-administrator', now() FROM identity_users WHERE \"UserType\" = 'platform' " +
                "ON CONFLICT DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_role_assignments");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_role",
                table: "role_assignments");

            migrationBuilder.DropIndex(
                name: "ux_identity_users_staff_email",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "identity_users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_role",
                table: "role_assignments",
                sql: "\"Role\" IN ('identity.administrator')");

            migrationBuilder.CreateIndex(
                name: "IX_identity_users_NormalizedEmail",
                table: "identity_users",
                column: "NormalizedEmail",
                unique: true);
        }
    }
}
