using ManpowerManagement.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerManagement.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260920000000_AddEmployeeShift")]
public partial class AddEmployeeShift : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Shift",
            schema: "mp",
            table: "Employees",
            type: "nvarchar(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "Day");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Shift", schema: "mp", table: "Employees");
    }
}
