using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

public class GradesController : BaseTeacherController
{
    public GradesController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    private static readonly string[] Periods = { "Midterm", "Finals" };
    private static readonly string[] Types   = { "Activity", "Quiz", "Exam", "Project" };

    // GET: /Grades
    public async Task<IActionResult> Index()
    {
        var t = await CurrentTeacherAsync();
        var subjects = await _db.Subjects
            .Where(s => s.TeacherId == t.TeacherId)
            .OrderBy(s => s.SubjectCode)
            .ToListAsync();
        return View(subjects);
    }

    // GET: /Grades/Subject/5
    public async Task<IActionResult> Subject(int id, string period = "Midterm")
    {
        var t = await CurrentTeacherAsync();

        var subject = await _db.Subjects
            .Include(s => s.Schedules)
            .FirstOrDefaultAsync(s => s.SubjectId == id && s.TeacherId == t.TeacherId);

        if (subject == null) return NotFound();

        var students = await _db.StudentSubjects
            .Include(ss => ss.Student)
            .Where(ss => ss.SubjectId == id && ss.Student!.TeacherId == t.TeacherId)
            .Select(ss => ss.Student!)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var columns = await _db.GradeColumns
            .Where(c => c.SubjectId == id)
            .OrderBy(c => c.Period)
            .ThenBy(c => c.Type)
            .ThenBy(c => c.Order)
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();
        var columnIds  = columns.Select(c => c.GradeColumnId).ToList();

        var grades = await _db.StudentGrades
            .Where(g => columnIds.Contains(g.GradeColumnId) && studentIds.Contains(g.StudentId))
            .ToListAsync();

        var scores = students.ToDictionary(
            s => s.StudentId,
            s => columns.ToDictionary(
                c => c.GradeColumnId,
                c => grades.FirstOrDefault(g => g.StudentId == s.StudentId && g.GradeColumnId == c.GradeColumnId)?.Score
            )
        );

        var midtermWeight = await _db.GradeWeights
            .FirstOrDefaultAsync(w => w.SubjectId == id && w.Period == "Midterm")
            ?? new GradeWeight { SubjectId = id, Period = "Midterm", ActivityWeight = 25, QuizWeight = 25, ExamWeight = 25, ProjectWeight = 25, MidtermWeight = 50, FinalsWeight = 50 };

        var finalsWeight = await _db.GradeWeights
            .FirstOrDefaultAsync(w => w.SubjectId == id && w.Period == "Finals")
            ?? new GradeWeight { SubjectId = id, Period = "Finals", ActivityWeight = 25, QuizWeight = 25, ExamWeight = 25, ProjectWeight = 25 };

        var midtermGrades = new Dictionary<int, decimal?>();
        var finalsGrades  = new Dictionary<int, decimal?>();
        var finalGrades   = new Dictionary<int, decimal?>();

        foreach (var s in students)
        {
            var mg = ComputePeriodGrade(s.StudentId, "Midterm", columns, scores, midtermWeight);
            var fg = ComputePeriodGrade(s.StudentId, "Finals",  columns, scores, finalsWeight);

            midtermGrades[s.StudentId] = mg;
            finalsGrades[s.StudentId]  = fg;

            if (mg.HasValue && fg.HasValue)
            {
                var raw = (mg.Value * midtermWeight.MidtermWeight / 100m)
                        + (fg.Value * midtermWeight.FinalsWeight / 100m);
                finalGrades[s.StudentId] = Math.Round(raw, 2);
            }
            else
            {
                finalGrades[s.StudentId] = null;
            }
        }

        var vm = new GradeIndexViewModel
        {
            Subject        = subject,
            Students       = students,
            Columns        = columns,
            Scores         = scores,
            MidtermWeight  = midtermWeight,
            FinalsWeight   = finalsWeight,
            MidtermGrades  = midtermGrades,
            FinalsGrades   = finalsGrades,
            FinalGrades    = finalGrades,
        };

        ViewBag.Period  = period;
        ViewBag.Periods = Periods;
        ViewBag.Types   = Types;

        return View(vm);
    }

    // POST: /Grades/AddColumn
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddColumn(int subjectId, AddGradeColumnViewModel m)
    {
        var t = await CurrentTeacherAsync();

        var subject = await _db.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId && s.TeacherId == t.TeacherId);

        if (subject == null) return NotFound();

        if (!Periods.Contains(m.Period) || !Types.Contains(m.Type))
            return BadRequest();

        var order = await _db.GradeColumns
            .Where(c => c.SubjectId == subjectId && c.Period == m.Period && c.Type == m.Type)
            .CountAsync();

        _db.GradeColumns.Add(new GradeColumn
        {
            SubjectId = subjectId,
            Period    = m.Period,
            Type      = m.Type,
            Title     = m.Title,
            MaxScore  = m.MaxScore,
            Order     = order + 1
        });

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Subject), new { id = subjectId, period = m.Period });
    }

    // POST: /Grades/DeleteColumn
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteColumn(int columnId, int subjectId, string period)
    {
        var t = await CurrentTeacherAsync();

        var col = await _db.GradeColumns
            .Include(c => c.Subject)
            .FirstOrDefaultAsync(c => c.GradeColumnId == columnId && c.Subject!.TeacherId == t.TeacherId);

        if (col != null)
        {
            _db.GradeColumns.Remove(col);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Subject), new { id = subjectId, period });
    }

    // POST: /Grades/SaveScores
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveScores(int subjectId, string period, IFormCollection form)
    {
        var t = await CurrentTeacherAsync();

        var columnIds = await _db.GradeColumns
            .Where(c => c.SubjectId == subjectId && c.Period == period && c.Subject!.TeacherId == t.TeacherId)
            .Select(c => c.GradeColumnId)
            .ToListAsync();

        var studentIds = await _db.StudentSubjects
            .Where(ss => ss.SubjectId == subjectId && ss.Student!.TeacherId == t.TeacherId)
            .Select(ss => ss.StudentId)
            .ToListAsync();

        var existing = await _db.StudentGrades
            .Where(g => columnIds.Contains(g.GradeColumnId) && studentIds.Contains(g.StudentId))
            .ToListAsync();

        foreach (var key in form.Keys)
        {
            // key format: scores[studentId][columnId]
            var match = System.Text.RegularExpressions.Regex.Match(key, @"scores\[(\d+)\]\[(\d+)\]");
            if (!match.Success) continue;

            var studentId = int.Parse(match.Groups[1].Value);
            var columnId  = int.Parse(match.Groups[2].Value);

            if (!studentIds.Contains(studentId) || !columnIds.Contains(columnId)) continue;

            decimal? score = decimal.TryParse(form[key], out var parsed) ? parsed : null;

            var rec = existing.FirstOrDefault(g => g.StudentId == studentId && g.GradeColumnId == columnId);
            if (rec == null)
            {
                _db.StudentGrades.Add(new StudentGrade
                {
                    StudentId     = studentId,
                    GradeColumnId = columnId,
                    Score         = score
                });
            }
            else
            {
                rec.Score = score;
            }
        }

        await _db.SaveChangesAsync();
        TempData["msg"] = "Grades saved.";
        return RedirectToAction(nameof(Subject), new { id = subjectId, period });
    }

    // POST: /Grades/SaveWeights
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWeights(
        int subjectId,
        string period,
        decimal activityWeight,
        decimal quizWeight,
        decimal examWeight,
        decimal projectWeight,
        decimal midtermWeight = 50,
        decimal finalsWeight  = 50)
    {
        var t = await CurrentTeacherAsync();

        var subject = await _db.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId && s.TeacherId == t.TeacherId);

        if (subject == null) return NotFound();

        var w = await _db.GradeWeights
            .FirstOrDefaultAsync(x => x.SubjectId == subjectId && x.Period == period);

        if (w == null)
        {
            w = new GradeWeight { SubjectId = subjectId, Period = period };
            _db.GradeWeights.Add(w);
        }

        w.ActivityWeight = activityWeight;
        w.QuizWeight     = quizWeight;
        w.ExamWeight     = examWeight;
        w.ProjectWeight  = projectWeight;

        if (period == "Midterm")
        {
            w.MidtermWeight = midtermWeight;
            w.FinalsWeight  = finalsWeight;
        }

        await _db.SaveChangesAsync();

        TempData["msg"] = "Weights saved.";

        return RedirectToAction(nameof(Subject), new { id = subjectId, period });
    }

    // helpers
    private static decimal? ComputePeriodGrade(
        int studentId,
        string period,
        List<GradeColumn> columns,
        Dictionary<int, Dictionary<int, decimal?>> scores,
        GradeWeight weight)
    {
        var periodCols = columns.Where(c => c.Period == period).ToList();
        if (!periodCols.Any()) return null;

        var typeWeights = new Dictionary<string, decimal>
        {
            ["Activity"] = weight.ActivityWeight,
            ["Quiz"]     = weight.QuizWeight,
            ["Exam"]     = weight.ExamWeight,
            ["Project"]  = weight.ProjectWeight,
        };

        decimal total   = 0;
        decimal totalWt = 0;

        foreach (var type in Types)
        {
            var cols = periodCols.Where(c => c.Type == type).ToList();
            if (!cols.Any()) continue;

            var typeAvg = cols
                .Select(c => scores.TryGetValue(studentId, out var s) && s.TryGetValue(c.GradeColumnId, out var sc) && sc.HasValue
                    ? (decimal?)(sc.Value / c.MaxScore * 100m)
                    : null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .DefaultIfEmpty(-1)
                .Average();

            if (typeAvg < 0) continue;

            total   += typeAvg * typeWeights[type];
            totalWt += typeWeights[type];
        }

        if (totalWt == 0) return null;

        var percentage = total / totalWt;
        return ConvertToPhilippineScale(percentage);
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
            _     => 5.0m,
        };
    }
}