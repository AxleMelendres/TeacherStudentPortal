using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;

namespace TeacherPortal.Controllers;

public class ReportsController : BaseTeacherController
{
    public ReportsController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    public async Task<IActionResult> AttendanceReport()
    {
        var t = await CurrentTeacherAsync();
        var data = await _db.Attendances.Include(a => a.Student).Include(a => a.Subject)
            .Where(a => a.Student!.TeacherId == t.TeacherId)
            .OrderByDescending(a => a.AttendanceDate).ToListAsync();
        return View(data);
    }

    public async Task<IActionResult> GradeReport()
    {
        var t = await CurrentTeacherAsync();
        var data = await _db.Grades.Include(g => g.Student).Include(g => g.Subject)
            .Where(g => g.Student!.TeacherId == t.TeacherId)
            .OrderBy(g => g.Student!.FullName).ToListAsync();
        return View(data);
    }

    public async Task<IActionResult> StudentPerformance()
    {
        var t = await CurrentTeacherAsync();
        var students = await _db.Students.Where(s => s.TeacherId == t.TeacherId).ToListAsync();
        var grades = await _db.Grades.Include(g => g.Subject).Where(g => g.Student!.TeacherId == t.TeacherId).ToListAsync();
        var rows = students.Select(s => new {
            Student = s,
            Average = grades.Where(g => g.StudentId == s.StudentId).Select(g => (decimal?)g.FinalGrade).DefaultIfEmpty(null).Average(),
            Subjects = grades.Count(g => g.StudentId == s.StudentId),
            Passed = grades.Count(g => g.StudentId == s.StudentId && g.Remarks == "Passed"),
            Failed = grades.Count(g => g.StudentId == s.StudentId && g.Remarks == "Failed")
        }).ToList();
        ViewBag.Rows = rows;
        return View();
    }
}
