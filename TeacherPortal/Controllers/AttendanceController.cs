using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;

namespace TeacherPortal.Controllers;

public class AttendanceController : BaseTeacherController
{
    public AttendanceController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    public static readonly string[] Statuses = { "Present", "Absent", "Late", "Excused" };

    public async Task<IActionResult> Index(int? subjectId, string? search, string? status, DateTime? date)
    {
        var t = await CurrentTeacherAsync();

        var subjects = await _db.Subjects
            .Where(s => s.TeacherId == t.TeacherId)
            .Include(s => s.Schedules)
            .OrderBy(s => s.SubjectCode)
            .ToListAsync();

        var subjectIds = subjects.Select(s => s.SubjectId).ToList();

        var allAttendances = await _db.Attendances
            .Include(a => a.Student)
            .Include(a => a.Subject)
            .Where(a =>
                a.Student != null &&
                a.Subject != null &&
                a.Student.TeacherId == t.TeacherId &&
                a.Subject.TeacherId == t.TeacherId)
            .OrderByDescending(a => a.AttendanceDate)
            .ThenBy(a => a.Student!.FullName)
            .ToListAsync();

        var recordCounts = allAttendances
            .GroupBy(a => a.SubjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        var presentCounts = allAttendances
            .Where(a => a.Status == "Present")
            .GroupBy(a => a.SubjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        var absentCounts = allAttendances
            .Where(a => a.Status == "Absent")
            .GroupBy(a => a.SubjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        var lateCounts = allAttendances
            .Where(a => a.Status == "Late")
            .GroupBy(a => a.SubjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        IEnumerable<Attendance> filtered = allAttendances;

        if (subjectId.HasValue && subjectId.Value > 0)
            filtered = filtered.Where(a => a.SubjectId == subjectId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(a =>
                (a.Student?.FullName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(status))
            filtered = filtered.Where(a => a.Status == status);

        if (date.HasValue)
        {
            var d = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, 0, 0, 0, DateTimeKind.Utc);
            filtered = filtered.Where(a => a.AttendanceDate == d);
        }

        ViewBag.Subjects = subjects;
        ViewBag.RecordCounts = recordCounts;
        ViewBag.PresentCounts = presentCounts;
        ViewBag.AbsentCounts = absentCounts;
        ViewBag.LateCounts = lateCounts;
        ViewBag.SelectedSubjectId = subjectId ?? 0;
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Date = date?.ToString("yyyy-MM-dd");

        return View(filtered.ToList());
    }

    public async Task<IActionResult> Create()
    {
        var t = await CurrentTeacherAsync();

        ViewBag.Students = new SelectList(
            await _db.Students
                .Where(s => s.TeacherId == t.TeacherId)
                .OrderBy(s => s.FullName)
                .ToListAsync(),
            "StudentId",
            "FullName"
        );

        ViewBag.Subjects = new SelectList(
            await _db.Subjects
                .Where(s => s.TeacherId == t.TeacherId)
                .OrderBy(s => s.SubjectCode)
                .ToListAsync(),
            "SubjectId",
            "SubjectName"
        );

        ViewBag.Sections = new SelectList(
            await _db.Sections
                .Where(s => s.TeacherId == t.TeacherId)
                .OrderBy(s => s.SectionName)
                .ToListAsync(),
            "SectionId",
            "SectionName"
        );

        ViewBag.Statuses = new SelectList(Statuses);

        return View(new Attendance
        {
            AttendanceDate = DateTime.UtcNow.Date
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Attendance m)
    {
        if (!Statuses.Contains(m.Status))
            ModelState.AddModelError(nameof(m.Status), "Invalid status");

        if (!ModelState.IsValid)
            return await Create();

        var t = await CurrentTeacherAsync();

        var enrolled = await IsStudentEnrolledInSubjectAsync(t.TeacherId, m.StudentId, m.SubjectId);

        if (!enrolled)
        {
            ModelState.AddModelError("", "Student is not enrolled in this subject.");
            return await Create();
        }

        var attendanceDate = ToUtcDate(m.AttendanceDate);
        var nextDate = attendanceDate.AddDays(1);

        m.AttendanceDate = attendanceDate;
        m.HasExcuseLetter = m.Status == "Absent" && m.HasExcuseLetter;

        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a =>
                a.SubjectId == m.SubjectId &&
                a.StudentId == m.StudentId &&
                a.AttendanceDate >= attendanceDate &&
                a.AttendanceDate < nextDate);

        if (existing == null)
            _db.Attendances.Add(m);
        else
        {
            existing.Status = m.Status;
            existing.HasExcuseLetter = m.Status == "Absent" && m.HasExcuseLetter;
            existing.Remarks = m.Remarks;
            existing.SectionId = m.SectionId;
            existing.AttendanceDate = attendanceDate;
        }

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var t = await CurrentTeacherAsync();

        var a = await _db.Attendances
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x =>
                x.AttendanceId == id &&
                x.Student != null &&
                x.Student.TeacherId == t.TeacherId);

        if (a == null) return NotFound();

        ViewBag.Statuses = new SelectList(Statuses, a.Status);

        return View(a);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Attendance input)
    {
        var t = await CurrentTeacherAsync();

        var a = await _db.Attendances
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x =>
                x.AttendanceId == id &&
                x.Student != null &&
                x.Student.TeacherId == t.TeacherId);

        if (a == null) return NotFound();

        if (!Statuses.Contains(input.Status))
        {
            ModelState.AddModelError(nameof(input.Status), "Invalid status");
            ViewBag.Statuses = new SelectList(Statuses, a.Status);
            return View(a);
        }

        a.Status = input.Status;
        a.HasExcuseLetter = input.Status == "Absent" && input.HasExcuseLetter;
        a.Remarks = input.Remarks;
        a.AttendanceDate = ToUtcDate(input.AttendanceDate);

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var t = await CurrentTeacherAsync();

        var a = await _db.Attendances
            .Include(x => x.Student)
            .Include(x => x.Subject)
            .FirstOrDefaultAsync(x =>
                x.AttendanceId == id &&
                x.Student != null &&
                x.Student.TeacherId == t.TeacherId);

        return a == null ? NotFound() : View(a);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var t = await CurrentTeacherAsync();

        var a = await _db.Attendances
            .Include(x => x.Student)
            .Include(x => x.Subject)
            .FirstOrDefaultAsync(x =>
                x.AttendanceId == id &&
                x.Student != null &&
                x.Student.TeacherId == t.TeacherId);

        return a == null ? NotFound() : View(a);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var t = await CurrentTeacherAsync();

        var a = await _db.Attendances
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x =>
                x.AttendanceId == id &&
                x.Student != null &&
                x.Student.TeacherId == t.TeacherId);

        if (a != null)
        {
            _db.Attendances.Remove(a);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> AttendanceSheet(int? subjectId, int? sectionId, DateTime? date)
    {
        var t = await CurrentTeacherAsync();

        var d = ToUtcDate(date ?? DateTime.UtcNow);
        var nextDate = d.AddDays(1);

        ViewBag.SubjectId = subjectId ?? 0;
        ViewBag.SectionId = sectionId;
        ViewBag.Date = d;
        ViewBag.Existing = new Dictionary<int, Attendance>();
        ViewBag.Statuses = Statuses;

        ViewBag.Subjects = new SelectList(
            await _db.Subjects
                .Where(s => s.TeacherId == t.TeacherId)
                .OrderBy(s => s.SubjectCode)
                .ToListAsync(),
            "SubjectId",
            "SubjectName",
            subjectId
        );

        ViewBag.Sections = new SelectList(
            await _db.Sections
                .Where(s => s.TeacherId == t.TeacherId)
                .OrderBy(s => s.SectionName)
                .ToListAsync(),
            "SectionId",
            "SectionName",
            sectionId
        );

        if (!subjectId.HasValue || subjectId.Value <= 0)
            return View(Array.Empty<Student>());

        var subjectExists = await _db.Subjects
            .AnyAsync(s => s.SubjectId == subjectId.Value && s.TeacherId == t.TeacherId);

        if (!subjectExists)
            return View(Array.Empty<Student>());

        var students = await _db.StudentSubjects
            .Include(ss => ss.Student)
            .Where(ss =>
                ss.SubjectId == subjectId.Value &&
                ss.Student != null &&
                ss.Student.TeacherId == t.TeacherId &&
                (!sectionId.HasValue || ss.Student.SectionId == sectionId))
            .Select(ss => ss.Student!)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();

        var existing = await _db.Attendances
            .Where(a =>
                a.SubjectId == subjectId.Value &&
                a.AttendanceDate >= d &&
                a.AttendanceDate < nextDate &&
                studentIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => g.OrderByDescending(a => a.AttendanceId).First())
            .ToListAsync();

        ViewBag.SubjectId = subjectId.Value;
        ViewBag.Existing = existing.ToDictionary(a => a.StudentId, a => a);

        return View(students);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSheet(
        int subjectId,
        int? sectionId,
        DateTime date,
        Dictionary<int, string> status,
        Dictionary<int, string>? remarks,
        Dictionary<int, bool>? hasExcuseLetter)
    {
        var t = await CurrentTeacherAsync();

        var students = await _db.StudentSubjects
            .Include(ss => ss.Student)
            .Where(ss =>
                ss.SubjectId == subjectId &&
                ss.Student != null &&
                ss.Student.TeacherId == t.TeacherId &&
                status.Keys.Contains(ss.StudentId))
            .Select(ss => ss.StudentId)
            .ToListAsync();

        var attendanceDate = ToUtcDate(date);
        var nextDate = attendanceDate.AddDays(1);

        var existing = await _db.Attendances
            .Where(a =>
                a.SubjectId == subjectId &&
                a.AttendanceDate >= attendanceDate &&
                a.AttendanceDate < nextDate &&
                students.Contains(a.StudentId))
            .ToListAsync();

        foreach (var sid in students)
        {
            if (!Statuses.Contains(status[sid]))
                continue;

            var rec = existing.FirstOrDefault(x => x.StudentId == sid);

            var hasLetter =
                status[sid] == "Absent" &&
                hasExcuseLetter != null &&
                hasExcuseLetter.ContainsKey(sid) &&
                hasExcuseLetter[sid];

            var studentRemarks =
                remarks != null && remarks.ContainsKey(sid)
                    ? remarks[sid]
                    : null;

            if (rec == null)
            {
                _db.Attendances.Add(new Attendance
                {
                    StudentId = sid,
                    SubjectId = subjectId,
                    SectionId = sectionId,
                    AttendanceDate = attendanceDate,
                    Status = status[sid],
                    HasExcuseLetter = hasLetter,
                    Remarks = studentRemarks
                });
            }
            else
            {
                rec.Status = status[sid];
                rec.HasExcuseLetter = hasLetter;
                rec.Remarks = studentRemarks;
                rec.SectionId = sectionId;
                rec.AttendanceDate = attendanceDate;
            }
        }

        await _db.SaveChangesAsync();

        TempData["msg"] = "Attendance saved.";

        return RedirectToAction(nameof(AttendanceSheet), new
        {
            subjectId,
            sectionId,
            date = attendanceDate.ToString("yyyy-MM-dd")
        });
    }

    private static DateTime ToUtcDate(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private async Task<bool> IsStudentEnrolledInSubjectAsync(int teacherId, int studentId, int subjectId)
    {
        return await _db.StudentSubjects
            .AnyAsync(ss =>
                ss.StudentId == studentId &&
                ss.SubjectId == subjectId &&
                ss.Student != null &&
                ss.Subject != null &&
                ss.Student.TeacherId == teacherId &&
                ss.Subject.TeacherId == teacherId);
    }
}