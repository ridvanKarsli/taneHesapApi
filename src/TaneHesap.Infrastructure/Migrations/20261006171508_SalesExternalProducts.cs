using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaneHesap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesExternalProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailySalesEntries_BusinessId",
                table: "DailySalesEntries");

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderNumber",
                table: "DailySalesEntries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "DailySalesEntries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailySalesEntries_BusinessId_ExternalOrderNumber",
                table: "DailySalesEntries",
                columns: new[] { "BusinessId", "ExternalOrderNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailySalesEntries_BusinessId_ExternalOrderNumber",
                table: "DailySalesEntries");

            migrationBuilder.DropColumn(
                name: "ExternalOrderNumber",
                table: "DailySalesEntries");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "DailySalesEntries");

            migrationBuilder.CreateIndex(
                name: "IX_DailySalesEntries_BusinessId",
                table: "DailySalesEntries",
                column: "BusinessId");
        }
    }
}
