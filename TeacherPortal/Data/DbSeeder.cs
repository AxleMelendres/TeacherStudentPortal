using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Models;

namespace TeacherPortal.Data;

public static class DbSeeder
{
    public const string TeacherRole = "Teacher";
    public const string StudentRole = "Student";

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Attendances\" ADD COLUMN IF NOT EXISTS \"HasExcuseLetter\" boolean NOT NULL DEFAULT FALSE;");

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { TeacherRole, StudentRole })
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));

        // Sample teacher
        var teacherEmail = "teacher@ccs.edu";
        var teacherUser = await userMgr.FindByEmailAsync(teacherEmail);
        if (teacherUser == null)
        {
            teacherUser = new ApplicationUser { UserName = teacherEmail, Email = teacherEmail, FullName = "Prof. Maria Santos", Role = TeacherRole, EmailConfirmed = true };
            await userMgr.CreateAsync(teacherUser, "Teacher@123");
            await userMgr.AddToRoleAsync(teacherUser, TeacherRole);
        }

        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.UserId == teacherUser.Id);
        if (teacher == null)
        {
            teacher = new Teacher { UserId = teacherUser.Id, EmployeeNumber = "CCS-001" };
            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();
        }

        if (!await db.Sections.AnyAsync(s => s.TeacherId == teacher.TeacherId))
        {
            db.Sections.AddRange(
                new Section { TeacherId = teacher.TeacherId, SectionName = "BSCS 2-A", YearLevel = "2nd Year", Description = "Computer Science Block A" },
                new Section { TeacherId = teacher.TeacherId, SectionName = "BSIT 3-B", YearLevel = "3rd Year", Description = "Information Tech Block B" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Subjects.AnyAsync(s => s.TeacherId == teacher.TeacherId))
        {
            db.Subjects.AddRange(
                new Subject { TeacherId = teacher.TeacherId, SubjectCode = "CS101", SubjectName = "Introduction to Programming", Description = "Fundamentals of programming" },
                new Subject { TeacherId = teacher.TeacherId, SubjectCode = "CS210", SubjectName = "Data Structures", Description = "Linear and non-linear structures" },
                new Subject { TeacherId = teacher.TeacherId, SubjectCode = "IT220", SubjectName = "Database Systems", Description = "Relational databases and SQL" }
            );
            await db.SaveChangesAsync();
        }

        var section = await db.Sections.FirstAsync(s => s.TeacherId == teacher.TeacherId);
        var subjects = await db.Subjects.Where(s => s.TeacherId == teacher.TeacherId).ToListAsync();

        var sampleStudents = new[]
        {
            ("2024-0001", "Juan Dela Cruz", "juan@ccs.edu"),
            ("2024-0002", "Ana Reyes", "ana@ccs.edu"),
            ("2024-0003", "Mark Villanueva", "mark@ccs.edu"),
            ("2024-0004", "Liza Gonzales", "liza@ccs.edu"),
            ("2024-0005", "Carlo Mendoza", "carlo@ccs.edu"),
            ("2024-0006", "Mika Santos", "mika@ccs.edu"),
            ("2024-0007", "Paolo Garcia", "paolo@ccs.edu"),
            ("2024-0008", "Rhea Bautista", "rhea@ccs.edu"),
            ("2024-0009", "Nico Ramirez", "nico@ccs.edu"),
            ("2024-0010", "Ella Fernandez", "ella@ccs.edu"),
            ("2024-0011", "Kevin Lim", "kevin@ccs.edu"),
            ("2024-0012", "Sofia Cruz", "sofia@ccs.edu"),
            ("2024-0013", "Miguel Aquino", "miguel@ccs.edu"),
            ("2024-0014", "Janelle Torres", "janelle@ccs.edu"),
            ("2024-0015", "Andre Castillo", "andre@ccs.edu"),
        };

        foreach (var (num, name, email) in sampleStudents)
        {
            var u = await userMgr.FindByEmailAsync(email);
            if (u == null)
            {
                u = new ApplicationUser { UserName = email, Email = email, FullName = name, Role = StudentRole, EmailConfirmed = true };
                await userMgr.CreateAsync(u, "Student@123");
            }

            if (!await userMgr.IsInRoleAsync(u, StudentRole))
                await userMgr.AddToRoleAsync(u, StudentRole);

            if (u.Role != StudentRole || u.FullName != name)
            {
                u.Role = StudentRole;
                u.FullName = name;
                await userMgr.UpdateAsync(u);
            }

            var st = await db.Students.FirstOrDefaultAsync(s => s.Email == email || s.StudentNumber == num || s.UserId == u.Id);
            if (st == null)
            {
                st = new Student { UserId = u.Id, StudentNumber = num, FullName = name, Email = email, ContactNumber = "09171234567", SectionId = section.SectionId, TeacherId = teacher.TeacherId };
                db.Students.Add(st);
                await db.SaveChangesAsync();
            }
            else
            {
                st.UserId = u.Id;
                st.StudentNumber = num;
                st.FullName = name;
                st.Email = email;
                st.TeacherId = teacher.TeacherId;
                st.SectionId ??= section.SectionId;
                await db.SaveChangesAsync();
            }

            foreach (var subj in subjects)
            {
                if (!await db.StudentSubjects.AnyAsync(ss => ss.StudentId == st.StudentId && ss.SubjectId == subj.SubjectId))
                    db.StudentSubjects.Add(new StudentSubject { StudentId = st.StudentId, SubjectId = subj.SubjectId, SectionId = st.SectionId });

                if (!await db.Grades.AnyAsync(g => g.StudentId == st.StudentId && g.SubjectId == subj.SubjectId))
                {
                    var g = new Grade { StudentId = st.StudentId, SubjectId = subj.SubjectId, Quiz = 85, Activity = 88, Assignment = 90, MidtermExam = 82, FinalExam = 86 };
                    g.FinalGrade = (g.Quiz + g.Activity + g.Assignment + g.MidtermExam + g.FinalExam) / 5;
                    g.Remarks = g.FinalGrade >= 75 ? "Passed" : "Failed";
                    db.Grades.Add(g);
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
