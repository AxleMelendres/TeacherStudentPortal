using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Models;

namespace TeacherPortal.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SubjectSchedule> SubjectSchedules => Set<SubjectSchedule>();
    public DbSet<StudentSubject> StudentSubjects => Set<StudentSubject>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<GradeColumn> GradeColumns { get; set; }
    public DbSet<StudentGrade> StudentGrades { get; set; }
    public DbSet<GradeWeight> GradeWeights { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Student>().HasIndex(s => s.StudentNumber).IsUnique();
        b.Entity<Student>().HasIndex(s => s.Email).IsUnique();
        b.Entity<Subject>().HasIndex(x => new { x.TeacherId, x.SubjectCode }).IsUnique();
        b.Entity<SubjectSchedule>().HasIndex(x => new { x.SubjectId, x.Day, x.StartTime }).IsUnique();
        b.Entity<Attendance>().HasIndex(x => new { x.SubjectId, x.StudentId, x.AttendanceDate }).IsUnique();
        b.Entity<Section>().HasIndex(x => new { x.TeacherId, x.SectionName }).IsUnique();
        b.Entity<StudentSubject>().HasIndex(x => new { x.StudentId, x.SubjectId }).IsUnique();
        b.Entity<Teacher>().HasIndex(t => t.EmployeeNumber).IsUnique();

        b.Entity<Grade>().Property(g => g.Quiz).HasPrecision(5, 2);
        b.Entity<Grade>().Property(g => g.Activity).HasPrecision(5, 2);
        b.Entity<Grade>().Property(g => g.Assignment).HasPrecision(5, 2);
        b.Entity<Grade>().Property(g => g.MidtermExam).HasPrecision(5, 2);
        b.Entity<Grade>().Property(g => g.FinalExam).HasPrecision(5, 2);
        b.Entity<Grade>().Property(g => g.FinalGrade).HasPrecision(5, 2);
        b.Entity<Student>().Property(s => s.EnrollmentCode).HasMaxLength(20);

        b.Entity<Student>()
            .HasOne(s => s.Section).WithMany(sec => sec.Students)
            .HasForeignKey(s => s.SectionId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Student>()
            .HasOne(s => s.Teacher).WithMany()
            .HasForeignKey(s => s.TeacherId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Attendance>().HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Attendance>().HasOne(a => a.Subject).WithMany().HasForeignKey(a => a.SubjectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SubjectSchedule>().HasOne(x => x.Subject).WithMany(x => x.Schedules).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Grade>().HasOne(g => g.Student).WithMany().HasForeignKey(g => g.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Grade>().HasOne(g => g.Subject).WithMany().HasForeignKey(g => g.SubjectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<StudentSubject>().HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<StudentSubject>().HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
