using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserTypeLookup : Migration
    {
        private const string UserTypeTypeId = "c1efb8ef-8803-4c4f-82df-c22666971c5c";
        private const string StaffId = "0d78e82f-0268-4c57-8926-4f31edfe1b61";
        private const string CustomerId = "d5f29ef5-6ab3-4fe7-bcf9-eb778b259dd5";
        private const string PlatformId = "d02f19f1-99bf-4f89-afdc-9eaa9e4317fb";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lookup_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lookup_types", x => x.Id);
                    table.CheckConstraint("ck_lookup_types_code", "\"Code\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "lookup_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LookupTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lookup_values", x => x.Id);
                    table.CheckConstraint("ck_lookup_values_code", "\"Code\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "FK_lookup_values_lookup_types_LookupTypeId",
                        column: x => x.LookupTypeId,
                        principalTable: "lookup_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_lookup_types_code",
                table: "lookup_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lookup_values_type_name",
                table: "lookup_values",
                columns: new[] { "LookupTypeId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "ux_lookup_values_type_code",
                table: "lookup_values",
                columns: new[] { "LookupTypeId", "Code" },
                unique: true);

            migrationBuilder.Sql(
                $"INSERT INTO lookup_types (\"Id\", \"Code\", \"Name\") VALUES ('{UserTypeTypeId}', 'user-type', 'User type') ON CONFLICT DO NOTHING;"
                + " INSERT INTO lookup_values (\"Id\", \"Code\", \"Name\", \"LookupTypeId\") VALUES"
                + $" ('{StaffId}', 'staff', 'Staff', '{UserTypeTypeId}'),"
                + $" ('{CustomerId}', 'customer', 'Customer', '{UserTypeTypeId}'),"
                + $" ('{PlatformId}', 'platform', 'Platform', '{UserTypeTypeId}') ON CONFLICT DO NOTHING;");

            migrationBuilder.DropIndex(
                name: "ux_identity_users_customer_email",
                table: "identity_users");

            migrationBuilder.DropIndex(
                name: "ux_identity_users_staff_email",
                table: "identity_users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_identity_users_type_tenant",
                table: "identity_users");

            migrationBuilder.AddColumn<Guid>(
                name: "UserTypeId",
                table: "identity_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE identity_users SET \"UserTypeId\" = CASE \"UserType\""
                + $" WHEN 'staff' THEN '{StaffId}'::uuid WHEN 'customer' THEN '{CustomerId}'::uuid WHEN 'platform' THEN '{PlatformId}'::uuid END;");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserTypeId",
                table: "identity_users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "identity_users");

            migrationBuilder.CreateIndex(
                name: "IX_identity_users_UserTypeId",
                table: "identity_users",
                column: "UserTypeId");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_customer_email",
                table: "identity_users",
                columns: new[] { "TenantId", "NormalizedEmail" },
                unique: true,
                filter: $"\"UserTypeId\" = '{CustomerId}'");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_staff_email",
                table: "identity_users",
                column: "NormalizedEmail",
                unique: true,
                filter: $"\"UserTypeId\" <> '{CustomerId}'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_identity_users_type_tenant",
                table: "identity_users",
                sql: $"(\"UserTypeId\" = '{PlatformId}' AND \"TenantId\" IS NULL) OR (\"UserTypeId\" IN ('{StaffId}', '{CustomerId}') AND \"TenantId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_identity_users_user_type",
                table: "identity_users",
                column: "UserTypeId",
                principalTable: "lookup_values",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_identity_users_user_type",
                table: "identity_users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_identity_users_type_tenant",
                table: "identity_users");

            migrationBuilder.DropIndex(
                name: "ux_identity_users_customer_email",
                table: "identity_users");

            migrationBuilder.DropIndex(
                name: "ux_identity_users_staff_email",
                table: "identity_users");

            migrationBuilder.DropIndex(
                name: "IX_identity_users_UserTypeId",
                table: "identity_users");

            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "identity_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE identity_users SET \"UserType\" = CASE \"UserTypeId\""
                + $" WHEN '{StaffId}'::uuid THEN 'staff' WHEN '{CustomerId}'::uuid THEN 'customer' WHEN '{PlatformId}'::uuid THEN 'platform' END;");

            migrationBuilder.AlterColumn<string>(
                name: "UserType",
                table: "identity_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UserTypeId",
                table: "identity_users");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_customer_email",
                table: "identity_users",
                columns: new[] { "TenantId", "NormalizedEmail" },
                unique: true,
                filter: "\"UserType\" = 'customer'");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_staff_email",
                table: "identity_users",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"UserType\" <> 'customer'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_identity_users_type_tenant",
                table: "identity_users",
                sql: "(\"UserType\" = 'platform' AND \"TenantId\" IS NULL) OR (\"UserType\" IN ('staff', 'customer') AND \"TenantId\" IS NOT NULL)");

            migrationBuilder.DropTable(
                name: "lookup_values");

            migrationBuilder.DropTable(
                name: "lookup_types");
        }
    }
}
