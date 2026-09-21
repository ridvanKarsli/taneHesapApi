using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaneHesap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTotpColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotpEnabled",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotpSecret",
                schema: "identity",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TotpEnabled",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecret",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);
        }
    }
}
