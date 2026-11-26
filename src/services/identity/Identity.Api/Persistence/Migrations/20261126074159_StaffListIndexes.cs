using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "identity_users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_role_user",
                table: "role_assignments",
                columns: new[] { "TenantId", "Role", "UserId" });

            migrationBuilder.CreateIndex(
                name: "ix_identity_users_staff_list",
                table: "identity_users",
                columns: new[] { "TenantId", "NormalizedEmail", "Id" },
                filter: "\"UserTypeId\" = '0d78e82f-0268-4c57-8926-4f31edfe1b61'")
                .Annotation("Npgsql:IndexInclude", new[] { "Email", "DisabledAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_role_assignments_role_user",
                table: "role_assignments");

            migrationBuilder.DropIndex(
                name: "ix_identity_users_staff_list",
                table: "identity_users");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "identity_users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldCollation: "C");
        }
    }
}
