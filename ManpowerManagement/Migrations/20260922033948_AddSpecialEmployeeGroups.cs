using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialEmployeeGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecialGroups",
                schema: "mp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSpecialGroups",
                schema: "mp",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SpecialGroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSpecialGroups", x => new { x.EmployeeId, x.SpecialGroupId });
                    table.ForeignKey(
                        name: "FK_EmployeeSpecialGroups_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "mp",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeSpecialGroups_SpecialGroups_SpecialGroupId",
                        column: x => x.SpecialGroupId,
                        principalSchema: "mp",
                        principalTable: "SpecialGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSpecialGroups_SpecialGroupId",
                schema: "mp",
                table: "EmployeeSpecialGroups",
                column: "SpecialGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialGroups_Name",
                schema: "mp",
                table: "SpecialGroups",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeSpecialGroups",
                schema: "mp");

            migrationBuilder.DropTable(
                name: "SpecialGroups",
                schema: "mp");
        }
    }
}
