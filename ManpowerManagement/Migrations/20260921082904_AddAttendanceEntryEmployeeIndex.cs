using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceEntryEmployeeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceEntries_EmployeeId",
                schema: "mp",
                table: "AttendanceEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_EmployeeId_BusinessDate_Shift",
                schema: "mp",
                table: "AttendanceEntries",
                columns: new[] { "EmployeeId", "BusinessDate", "Shift" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceEntries_EmployeeId_BusinessDate_Shift",
                schema: "mp",
                table: "AttendanceEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_EmployeeId",
                schema: "mp",
                table: "AttendanceEntries",
                column: "EmployeeId");
        }
    }
}
