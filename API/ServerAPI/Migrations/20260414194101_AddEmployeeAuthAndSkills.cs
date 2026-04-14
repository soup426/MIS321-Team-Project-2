using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MulhollandRealEstate.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAuthAndSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "employees",
                type: "varchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "employees",
                type: "varchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "employee_skills",
                columns: table => new
                {
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    skill = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_skills", x => new { x.employee_id, x.skill });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "uq_employees_username",
                table: "employees",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_skills_skill",
                table: "employee_skills",
                column: "skill");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_skills");

            migrationBuilder.DropIndex(
                name: "uq_employees_username",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "username",
                table: "employees");
        }
    }
}
