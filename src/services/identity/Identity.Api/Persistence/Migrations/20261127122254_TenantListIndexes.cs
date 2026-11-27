using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "tenants",
                type: "character varying(63)",
                maxLength: 63,
                nullable: false,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "character varying(63)",
                oldMaxLength: 63);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "C");

            migrationBuilder.Sql("UPDATE tenants SET \"NormalizedName\" = upper(\"Name\");");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "C");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_normalized_name",
                table: "tenants",
                column: "NormalizedName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_normalized_name",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "tenants");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "tenants",
                type: "character varying(63)",
                maxLength: 63,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(63)",
                oldMaxLength: 63,
                oldCollation: "C");
        }
    }
}
