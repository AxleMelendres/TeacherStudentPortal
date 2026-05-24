using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

[Authorize(Roles = "Student")]
public class StudentDashboardController : Controller
{
    private static readonly string[] Days =
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    private static readonly string[] TimeSlots =
    {
        "7:30 AM - 9:00 AM",
        "9:00 AM - 10:30 AM",
        "10:30 AM - 12:00 PM",
        "1:00 PM - 2:30 PM",
        "2:30 PM - 4:00 PM",
        "4:00 PM - 5:30 PM"
    };

    private static readonly string[] Types = { "Activity", "Quiz", "Exam", "Project" };
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public StudentDashboardController(ApplicationDbContext db, UserManager<ApplicationUser> u) { _db = db; _users = u; }

    private async Task<Student?> CurrentStudentAsync()
    {
        var uid = _users.GetUserId(User);
        return await _db.Students.Include(s => s.Section).FirstOrDefaultAsync(s => s.UserId == uid);
    }

    public async Task<IActionResult> Index()
    {
        var s = await CurrentStudentAsync(); if (s == null) return NotFound();
        ViewBag.Student = s;
        ViewBag.Subjects = await _db.StudentSubjects.Include(x => x.Subject).Include(x => x.Section).Where(x => x.StudentId == s.StudentId).ToListAsync();
        ViewBag.Grades = await BuildStudentGradesAsync(s.StudentId);
        ViewBag.AttendanceCount = await _db.Attendances.CountAsync(a => a.StudentId == s.StudentId);
        ViewBag.PresentCount = await _db.Attendances.CountAsync(a => a.StudentId == s.StudentId && a.Status == "Present");
        return View();
    }

    public async Task<IActionResult> MySubjects()
    {
        var s = await CurrentStudentAsync(); if (s == null) return NotFound();
        var list = await _db.StudentSubjects
            .Include(x => x.Subject)
            .Include(x => x.Section)
            .Where(x => x.StudentId == s.StudentId)
            .OrderBy(x => x.Subject!.SubjectCode)
            .ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> MyGrades()
    {
        var s = await CurrentStudentAsync(); if (s == null) return NotFound();
        var list = await BuildStudentGradesAsync(s.StudentId);
        return View(list);
    }

    public async Task<IActionResult> MySchedule()
    {
        var s = await CurrentStudentAsync(); if (s == null) return NotFound();

        var enrollments = await _db.StudentSubjects
            .Include(x => x.Section)
            .Include(x => x.Subject)
                .ThenInclude(subject => subject!.Schedules)
            .Where(x => x.StudentId == s.StudentId)
            .OrderBy(x => x.Subject!.SubjectCode)
            .ToListAsync();

        var entries = new List<ScheduleEntryViewModel>();

        foreach (var enrollment in enrollments)
        {
            if (enrollment.Subject == null) continue;

            var schedules = GetEffectiveSchedules(enrollment.Subject);
            foreach (var schedule in schedules)
            {
                var dayIndex = Array.IndexOf(Days, schedule.Day);
                if (dayIndex < 0) continue;

                entries.Add(new ScheduleEntryViewModel
                {
                    DayOrder = dayIndex,
                    DayName = Days[dayIndex],
                    TimeSlot = FormatTimeSlot(schedule),
                    SubjectCode = enrollment.Subject.SubjectCode,
                    SubjectName = enrollment.Subject.SubjectName,
                    SectionName = enrollment.Section?.SectionName ?? s.Section?.SectionName ?? "No section",
                    Room = string.IsNullOrWhiteSpace(schedule.Room) ? "No room set" : schedule.Room
                });
            }
        }

        var timeSlots = entries.Select(e => e.TimeSlot)
            .Concat(TimeSlots)
            .Distinct()
            .OrderBy(NormalizeTimeSlot)
            .ToList();

        ViewBag.StudentName = s.FullName;
        ViewBag.StudentNumber = s.StudentNumber;
        ViewBag.SectionName = s.Section?.SectionName ?? "Unassigned";

        return View(new ScheduleIndexViewModel
        {
            Days = Days,
            TimeSlots = timeSlots,
            Entries = entries
        });
    }

    public async Task<IActionResult> MyAttendance(int? subjectId, DateTime? dateFrom, DateTime? dateTo)
    {
        var s = await CurrentStudentAsync(); if (s == null) return NotFound();

        var enrolledSubjects = await _db.StudentSubjects
            .Include(x => x.Subject)
            .Where(x => x.StudentId == s.StudentId)
            .OrderBy(x => x.Subject!.SubjectCode)
            .ToListAsync();

        var enrolledSubjectIds = enrolledSubjects.Select(x => x.SubjectId).ToList();
        var query = _db.Attendances
            .Include(a => a.Subject)
            .Where(a => a.StudentId == s.StudentId && enrolledSubjectIds.Contains(a.SubjectId));

        if (subjectId.HasValue && enrolledSubjectIds.Contains(subjectId.Value))
            query = query.Where(a => a.SubjectId == subjectId.Value);

        if (dateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.AttendanceDate >= from);
        }

        if (dateTo.HasValue)
        {
            var to = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(a => a.AttendanceDate < to);
        }

        ViewBag.Subjects = new SelectList(
            enrolledSubjects.Select(x => new
            {
                x.SubjectId,
                SubjectName = $"{x.Subject?.SubjectCode} - {x.Subject?.SubjectName}"
            }),
            "SubjectId",
            "SubjectName",
            subjectId);
        ViewBag.SubjectId = subjectId;
        ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
        ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

        var list = await query.OrderByDescending(a => a.AttendanceDate).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Profile()
    {
        var s = await CurrentStudentAsync(); if (s == null) return NotFound();
        return View(s);
    }

    private async Task<List<StudentPortalGradeViewModel>> BuildStudentGradesAsync(int studentId)
    {
        var enrolledSubjects = await _db.StudentSubjects
            .Include(x => x.Subject)
            .Include(x => x.Section)
            .Where(x => x.StudentId == studentId)
            .OrderBy(x => x.Subject!.SubjectCode)
            .ToListAsync();

        var subjectIds = enrolledSubjects.Select(x => x.SubjectId).ToList();
        var columns = await _db.GradeColumns
            .Where(c => subjectIds.Contains(c.SubjectId))
            .OrderBy(c => c.Period)
            .ThenBy(c => c.Type)
            .ThenBy(c => c.Order)
            .ToListAsync();

        var columnIds = columns.Select(c => c.GradeColumnId).ToList();
        var scores = await _db.StudentGrades
            .Where(g => g.StudentId == studentId && columnIds.Contains(g.GradeColumnId))
            .ToListAsync();

        var weights = await _db.GradeWeights
            .Where(w => subjectIds.Contains(w.SubjectId))
            .ToListAsync();

        var legacyGrades = await _db.Grades
            .Include(g => g.Subject)
            .Where(g => g.StudentId == studentId && subjectIds.Contains(g.SubjectId))
            .ToListAsync();

        var result = new List<StudentPortalGradeViewModel>();

        foreach (var enrollment in enrolledSubjects)
        {
            if (enrollment.Subject == null) continue;

            var subjectColumns = columns.Where(c => c.SubjectId == enrollment.SubjectId).ToList();
            var subjectScores = scores.Where(g => subjectColumns.Any(c => c.GradeColumnId == g.GradeColumnId)).ToList();
            var scoreItems = subjectColumns
                .Select(c => new StudentPortalScoreItemViewModel
                {
                    Period = c.Period,
                    Type = c.Type,
                    Title = c.Title,
                    MaxScore = c.MaxScore,
                    Score = subjectScores.FirstOrDefault(s => s.GradeColumnId == c.GradeColumnId)?.Score,
                    Order = c.Order
                })
                .ToList();

            if (subjectColumns.Any())
            {
                var midtermWeight = weights.FirstOrDefault(w => w.SubjectId == enrollment.SubjectId && w.Period == "Midterm")
                    ?? new GradeWeight { SubjectId = enrollment.SubjectId, Period = "Midterm", ActivityWeight = 25, QuizWeight = 25, ExamWeight = 25, ProjectWeight = 25, MidtermWeight = 50, FinalsWeight = 50 };

                var finalsWeight = weights.FirstOrDefault(w => w.SubjectId == enrollment.SubjectId && w.Period == "Finals")
                    ?? new GradeWeight { SubjectId = enrollment.SubjectId, Period = "Finals", ActivityWeight = 25, QuizWeight = 25, ExamWeight = 25, ProjectWeight = 25 };

                var midterm = ComputePeriodGrade("Midterm", subjectColumns, subjectScores, midtermWeight);
                var finals = ComputePeriodGrade("Finals", subjectColumns, subjectScores, finalsWeight);
                decimal? finalGrade = null;

                if (midterm.HasValue && finals.HasValue)
                {
                    var raw = (midterm.Value * midtermWeight.MidtermWeight / 100m)
                            + (finals.Value * midtermWeight.FinalsWeight / 100m);
                    finalGrade = Math.Round(raw, 2);
                }

                result.Add(new StudentPortalGradeViewModel
                {
                    SubjectId = enrollment.SubjectId,
                    SubjectCode = enrollment.Subject.SubjectCode,
                    SubjectName = enrollment.Subject.SubjectName,
                    SectionName = enrollment.Section?.SectionName ?? "No section",
                    MidtermGrade = midterm,
                    FinalsGrade = finals,
                    FinalGrade = finalGrade,
                    Remarks = finalGrade.HasValue ? (finalGrade.Value <= 3.0m ? "Passed" : "Failed") : "In Progress",
                    RecordedScores = subjectScores.Count(g => g.Score.HasValue),
                    TotalColumns = subjectColumns.Count,
                    UsesTeacherGradebook = true,
                    Scores = scoreItems
                });
            }
            else
            {
                var legacy = legacyGrades.FirstOrDefault(g => g.SubjectId == enrollment.SubjectId);
                var legacyScoreItems = legacy == null
                    ? new List<StudentPortalScoreItemViewModel>()
                    : new List<StudentPortalScoreItemViewModel>
                    {
                        new() { Period = "Midterm", Type = "Quiz", Title = "Quiz", MaxScore = 100, Score = legacy.Quiz, Order = 1 },
                        new() { Period = "Midterm", Type = "Activity", Title = "Activity", MaxScore = 100, Score = legacy.Activity, Order = 2 },
                        new() { Period = "Midterm", Type = "Activity", Title = "Assignment", MaxScore = 100, Score = legacy.Assignment, Order = 3 },
                        new() { Period = "Midterm", Type = "Exam", Title = "Midterm Exam", MaxScore = 100, Score = legacy.MidtermExam, Order = 4 },
                        new() { Period = "Finals", Type = "Exam", Title = "Final Exam", MaxScore = 100, Score = legacy.FinalExam, Order = 5 }
                    };

                result.Add(new StudentPortalGradeViewModel
                {
                    SubjectId = enrollment.SubjectId,
                    SubjectCode = enrollment.Subject.SubjectCode,
                    SubjectName = enrollment.Subject.SubjectName,
                    SectionName = enrollment.Section?.SectionName ?? "No section",
                    MidtermGrade = legacy?.MidtermExam,
                    FinalsGrade = legacy?.FinalExam,
                    FinalGrade = legacy?.FinalGrade,
                    Remarks = legacy?.Remarks ?? "In Progress",
                    RecordedScores = legacy == null ? 0 : 1,
                    TotalColumns = legacy == null ? 0 : 1,
                    UsesTeacherGradebook = false,
                    Scores = legacyScoreItems
                });
            }
        }

        return result;
    }

    private static decimal? ComputePeriodGrade(string period, List<GradeColumn> columns, List<StudentGrade> scores, GradeWeight weight)
    {
        var periodColumns = columns.Where(c => c.Period == period).ToList();
        if (!periodColumns.Any()) return null;

        var typeWeights = new Dictionary<string, decimal>
        {
            ["Activity"] = weight.ActivityWeight,
            ["Quiz"] = weight.QuizWeight,
            ["Exam"] = weight.ExamWeight,
            ["Project"] = weight.ProjectWeight,
        };

        decimal total = 0;
        decimal totalWeight = 0;

        foreach (var type in Types)
        {
            var typeColumns = periodColumns.Where(c => c.Type == type).ToList();
            if (!typeColumns.Any()) continue;

            var typeScores = typeColumns
                .Select(c =>
                {
                    var score = scores.FirstOrDefault(s => s.GradeColumnId == c.GradeColumnId)?.Score;
                    return score.HasValue ? (decimal?)(score.Value / c.MaxScore * 100m) : null;
                })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            if (!typeScores.Any()) continue;

            total += typeScores.Average() * typeWeights[type];
            totalWeight += typeWeights[type];
        }

        if (totalWeight == 0) return null;
        return ConvertToPhilippineScale(total / totalWeight);
    }

    private static decimal ConvertToPhilippineScale(decimal percentage)
    {
        return percentage switch
        {
            >= 99 => 1.0m,
            >= 96 => 1.25m,
            >= 93 => 1.5m,
            >= 90 => 1.75m,
            >= 87 => 2.0m,
            >= 84 => 2.25m,
            >= 81 => 2.5m,
            >= 78 => 2.75m,
            >= 75 => 3.0m,
            _ => 5.0m,
        };
    }

    private static IReadOnlyList<SubjectSchedule> GetEffectiveSchedules(Subject subject)
    {
        if (subject.Schedules.Any())
            return subject.Schedules.ToList();

        if (string.IsNullOrWhiteSpace(subject.ScheduleDay)
            || string.IsNullOrWhiteSpace(subject.ScheduleStartTime)
            || string.IsNullOrWhiteSpace(subject.ScheduleEndTime))
            return Array.Empty<SubjectSchedule>();

        return new[]
        {
            new SubjectSchedule
            {
                SubjectId = subject.SubjectId,
                Day = subject.ScheduleDay,
                StartTime = subject.ScheduleStartTime,
                EndTime = subject.ScheduleEndTime,
                Room = subject.Room
            }
        };
    }

    private static string FormatTimeSlot(SubjectSchedule schedule)
    {
        return $"{FormatTime(schedule.StartTime)} - {FormatTime(schedule.EndTime)}";
    }

    private static string FormatTime(string value)
    {
        return TimeOnly.TryParse(value, out var time)
            ? time.ToString("h:mm tt")
            : value;
    }

    private static TimeOnly NormalizeTimeSlot(string value)
    {
        var start = value.Split(" - ")[0];
        return TimeOnly.TryParse(start, out var time) ? time : TimeOnly.MaxValue;
    }
}
