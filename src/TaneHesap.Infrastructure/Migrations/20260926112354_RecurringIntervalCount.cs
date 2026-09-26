using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaneHesap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecurringIntervalCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalCount",
                table: "RecurringExpenses",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntervalCount",
                table: "RecurringExpenses");
        }
    }
}
