using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensorMon.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Component",
                table: "Readings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Component",
                table: "Readings");
        }
    }
}
