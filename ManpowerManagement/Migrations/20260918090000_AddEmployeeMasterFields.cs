using ManpowerManagement.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerManagement.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260918090000_AddEmployeeMasterFields")]
public partial class AddEmployeeMasterFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Accommodation", schema: "mp", table: "Employees", type: "nvarchar(max)", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "JobGrade", schema: "mp", table: "Employees", type: "nvarchar(max)", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Remark", schema: "mp", table: "Employees", type: "nvarchar(max)", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "TelephoneNumber", schema: "mp", table: "Employees", type: "nvarchar(max)", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Accommodation", schema: "mp", table: "Employees");
        migrationBuilder.DropColumn(name: "JobGrade", schema: "mp", table: "Employees");
        migrationBuilder.DropColumn(name: "Remark", schema: "mp", table: "Employees");
        migrationBuilder.DropColumn(name: "TelephoneNumber", schema: "mp", table: "Employees");
    }
}
