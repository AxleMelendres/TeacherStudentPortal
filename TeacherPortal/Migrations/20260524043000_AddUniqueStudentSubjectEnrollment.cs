using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TeacherPortal.Data;

#nullable disable

namespace TeacherPortal.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260524043000_AddUniqueStudentSubjectEnrollment")]
    public partial class AddUniqueStudentSubjectEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "StudentSubjects" a
                USING "StudentSubjects" b
                WHERE a."StudentSubjectId" > b."StudentSubjectId"
                  AND a."StudentId" = b."StudentId"
                  AND a."SubjectId" = b."SubjectId";
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_StudentSubjects_StudentId";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_StudentSubjects_StudentId_SubjectId"
                    ON "StudentSubjects" ("StudentId", "SubjectId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_StudentSubjects_StudentId_SubjectId";
                CREATE INDEX IF NOT EXISTS "IX_StudentSubjects_StudentId"
                    ON "StudentSubjects" ("StudentId");
                """);
        }
    }
}
