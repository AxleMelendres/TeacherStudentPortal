using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TeacherPortal.Data;

#nullable disable

namespace TeacherPortal.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260524050000_AllowStudentSelfRegistration")]
    public partial class AllowStudentSelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "Students" DROP CONSTRAINT IF EXISTS "FK_Students_Teachers_TeacherId";""");

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Students",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql("""
                ALTER TABLE "Students"
                ADD CONSTRAINT "FK_Students_Teachers_TeacherId"
                FOREIGN KEY ("TeacherId") REFERENCES "Teachers" ("TeacherId")
                ON DELETE SET NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "Students" DROP CONSTRAINT IF EXISTS "FK_Students_Teachers_TeacherId";""");

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.Sql("""
                ALTER TABLE "Students"
                ADD CONSTRAINT "FK_Students_Teachers_TeacherId"
                FOREIGN KEY ("TeacherId") REFERENCES "Teachers" ("TeacherId")
                ON DELETE CASCADE;
                """);
        }
    }
}
