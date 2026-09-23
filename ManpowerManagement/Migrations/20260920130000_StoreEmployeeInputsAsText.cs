using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManpowerManagement.Migrations
{
    /// <inheritdoc />
    public partial class StoreEmployeeInputsAsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StartDate",
                schema: "mp",
                table: "Employees",
                type: "nvarchar(10)",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "ResignedDate",
                schema: "mp",
                table: "Employees",
                type: "nvarchar(10)",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmploymentType",
                schema: "mp",
                table: "Employees",
                type: "nvarchar(32)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "EmploymentState",
                schema: "mp",
                table: "Employees",
                type: "nvarchar(32)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // Preserve existing enum values after changing the storage type.
            migrationBuilder.Sql("UPDATE [mp].[Employees] SET [EmploymentType] = CASE [EmploymentType] WHEN N'0' THEN N'NewComer' WHEN N'1' THEN N'Permanent' ELSE [EmploymentType] END;");
            migrationBuilder.Sql("UPDATE [mp].[Employees] SET [EmploymentState] = CASE [EmploymentState] WHEN N'0' THEN N'Active' WHEN N'1' THEN N'Resigned' ELSE [EmploymentState] END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [mp].[Employees] SET [EmploymentType] = CASE [EmploymentType] WHEN N'NewComer' THEN N'0' WHEN N'Permanent' THEN N'1' ELSE [EmploymentType] END;");
            migrationBuilder.Sql("UPDATE [mp].[Employees] SET [EmploymentState] = CASE [EmploymentState] WHEN N'Active' THEN N'0' WHEN N'Resigned' THEN N'1' ELSE [EmploymentState] END;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                schema: "mp",
                table: "Employees",
                type: "date",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ResignedDate",
                schema: "mp",
                table: "Employees",
                type: "date",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmploymentType",
                schema: "mp",
                table: "Employees",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)");

            migrationBuilder.AlterColumn<int>(
                name: "EmploymentState",
                schema: "mp",
                table: "Employees",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)");
        }
    }
}
