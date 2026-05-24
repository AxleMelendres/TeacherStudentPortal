using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TeacherPortal.Data;

#nullable disable

namespace TeacherPortal.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260524053000_AddStudentEnrollmentCode")]
    public partial class AddStudentEnrollmentCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnrollmentCode",
                table: "Students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Students"
                SET "EnrollmentCode" = UPPER(SUBSTRING(MD5(RANDOM()::TEXT || "StudentId"::TEXT), 1, 8))
                WHERE "EnrollmentCode" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrollmentCode",
                table: "Students");
        }
    }
}
