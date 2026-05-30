using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensorMon.Worker.Migrations
{
    /// <inheritdoc />
    public partial class StoreLocalTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename rather than drop+add so existing rows keep their data.
            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                table: "Readings",
                newName: "Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_Readings_TimestampUtc",
                table: "Readings",
                newName: "IX_Readings_Timestamp");

            // Change the column type from `timestamp with time zone` (UTC) to
            // `timestamp without time zone` (wall clock). Postgres converts
            // existing values using the session time zone.
            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Readings",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Readings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.RenameIndex(
                name: "IX_Readings_Timestamp",
                table: "Readings",
                newName: "IX_Readings_TimestampUtc");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "Readings",
                newName: "TimestampUtc");
        }
    }
}
