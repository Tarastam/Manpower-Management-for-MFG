using ManpowerManagement.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerManagement.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260920115941_AddEmployeeProcess")]
public partial class AddEmployeeProcess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Process",
            schema: "mp",
            table: "Employees",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Process", schema: "mp", table: "Employees");
    }
}
