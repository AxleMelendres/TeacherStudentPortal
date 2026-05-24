using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

public class SubjectsController : BaseTeacherController
{
    private static readonly string[] ScheduleDays =
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    public SubjectsController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    public async Task<IActionResult> Index()
    {
        var t = await CurrentTeacherAsync();

        var subjects = await _db.Subjects
            .Include(s => s.Schedules)
            .Where(s => s.TeacherId == t.TeacherId)
            .OrderBy(s => s.SubjectCode)
            .ToListAsync();

        var subjectIds = subjects.Select(s => s.SubjectId).ToList();

        ViewBag.EnrolledCounts = await _db.StudentSubjects
            .Where(ss => subjectIds.Contains(ss.SubjectId))
            .GroupBy(ss => ss.SubjectId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        return View(subjects);
    }

    public IActionResult Create()
    {
        ViewBag.ScheduleDays = ScheduleDays;

        return View(new SubjectFormViewModel
        {
            Schedules = EmptyScheduleRows()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubjectFormViewModel m)
    {
        ValidateSchedules(m.Schedules);

        var t = await CurrentTeacherAsync();

        if (ModelState.IsValid)
            await ValidateScheduleConflictsAsync(t.TeacherId, null, m.Schedules);

        if (!ModelState.IsValid)
        {
            ViewBag.ScheduleDays = ScheduleDays;
            return View(m);
        }

        var subject = new Subject
        {
            TeacherId = t.TeacherId,
            SubjectCode = m.SubjectCode,
            SubjectName = m.SubjectName,
            Description = null
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        await SaveSchedulesAsync(subject.SubjectId, m.Schedules);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var t = await CurrentTeacherAsync();

        var s = await _db.Subjects
            .Include(x => x.Schedules)
            .FirstOrDefaultAsync(x => x.SubjectId == id && x.TeacherId == t.TeacherId);

        ViewBag.ScheduleDays = ScheduleDays;

        if (s == null) return NotFound();

        return View(new SubjectFormViewModel
        {
            SubjectId = s.SubjectId,
            SubjectCode = s.SubjectCode,
            SubjectName = s.SubjectName,
            Description = s.Description,
            Schedules = ToScheduleRows(s.Schedules)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SubjectFormViewModel input)
    {
        var t = await CurrentTeacherAsync();

        var s = await _db.Subjects
            .FirstOrDefaultAsync(x => x.SubjectId == id && x.TeacherId == t.TeacherId);

        if (s == null) return NotFound();

        ValidateSchedules(input.Schedules);

        if (ModelState.IsValid)
            await ValidateScheduleConflictsAsync(t.TeacherId, id, input.Schedules);

        if (!ModelState.IsValid)
        {
            ViewBag.ScheduleDays = ScheduleDays;
            return View(input);
        }

        s.SubjectCode = input.SubjectCode;
        s.SubjectName = input.SubjectName;
        s.Description = null;

        await _db.SaveChangesAsync();
        await SaveSchedulesAsync(s.SubjectId, input.Schedules);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, DateTime? date)
    {
        var t = await CurrentTeacherAsync();

        var s = await _db.Subjects
            .Include(x => x.Schedules)
            .FirstOrDefaultAsync(x => x.SubjectId == id && x.TeacherId == t.TeacherId);

        if (s == null) return NotFound();

        var effectiveSchedules = GetEffectiveSchedules(s);
        var dateOptions = BuildAttendanceDateOptions(effectiveSchedules);
        var selectedDate = ResolveAttendanceDate(date, dateOptions);

        var enrolled = await _db.StudentSubjects
            .Include(x => x.Student)
                .ThenInclude(st => st!.Section)
            .Where(x => x.SubjectId == id && x.Student!.TeacherId == t.TeacherId)
            .OrderBy(x => x.Student!.FullName)
            .ToListAsync();

        var studentIds = enrolled.Select(x => x.StudentId).ToList();

        var dayStart = ToAttendanceDate(selectedDate);
        var dayEnd = dayStart.AddDays(1);

        var attendance = await _db.Attendances
            .Where(a =>
                a.SubjectId == id &&
                a.AttendanceDate >= dayStart &&
                a.AttendanceDate < dayEnd &&
                studentIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => g.OrderByDescending(a => a.AttendanceId).First())
            .ToDictionaryAsync(a => a.StudentId);

        var vm = new SubjectDetailsViewModel
        {
            Subject = s,
            AttendanceDate = selectedDate,
            AttendanceDateOptions = dateOptions,
            Students = enrolled.Select(x =>
            {
                attendance.TryGetValue(x.StudentId, out var record);

                return new SubjectStudentAttendanceViewModel
                {
                    StudentId = x.StudentId,
                    StudentNumber = x.Student?.StudentNumber ?? "",
                    FullName = x.Student?.FullName ?? "",
                    Email = x.Student?.Email ?? "",
                    SectionName = x.Student?.Section?.SectionName ?? "No section",
                    Status = record?.Status ?? "Not marked",
                    HasExcuseLetter = record?.HasExcuseLetter ?? false,
                    AttendanceId = record?.AttendanceId
                };
            }).ToList()
        };

        return View(vm);
    }

    // FIXED: Accept date as string to avoid timezone shifting during model binding
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAttendance(
        int subjectId,
        int studentId,
        string attendanceDate,
        string status,
        bool hasExcuseLetter = false)
    {
        if (status != "Present" && status != "Absent")
            return BadRequest();

        if (!DateOnly.TryParse(attendanceDate, out var parsedDate))
            return BadRequest("Invalid date.");

        var date = DateTime.SpecifyKind(parsedDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var t = await CurrentTeacherAsync();

        var subject = await _db.Subjects
            .Include(s => s.Schedules)
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId && s.TeacherId == t.TeacherId);

        var enrolled = await _db.StudentSubjects
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x =>
                x.SubjectId == subjectId &&
                x.StudentId == studentId &&
                x.Student!.TeacherId == t.TeacherId);

        if (subject == null || enrolled?.Student == null)
            return NotFound();

        if (!IsAllowedAttendanceDate(date, GetEffectiveSchedules(subject)))
            return BadRequest("Attendance date must match the subject schedule.");

        var sectionId = enrolled.SectionId ?? enrolled.Student.SectionId;
        var finalHasExcuseLetter = status == "Absent" && hasExcuseLetter;

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""Attendances""
                (""StudentId"", ""SubjectId"", ""SectionId"", ""AttendanceDate"", ""Status"", ""HasExcuseLetter"", ""Remarks"")
            VALUES
                ({studentId}, {subjectId}, {sectionId}, {date}, {status}, {finalHasExcuseLetter}, {null})
            ON CONFLICT (""SubjectId"", ""StudentId"", ""AttendanceDate"")
            DO UPDATE SET
                ""Status"" = EXCLUDED.""Status"",
                ""HasExcuseLetter"" = EXCLUDED.""HasExcuseLetter"",
                ""SectionId"" = EXCLUDED.""SectionId"";
        ");

        TempData["msg"] = status == "Absent" && hasExcuseLetter
            ? $"{enrolled.Student.FullName} marked Absent with excuse letter."
            : $"{enrolled.Student.FullName} marked {status}.";

        return RedirectToAction(nameof(Details), new
        {
            id = subjectId,
            date = date.ToString("yyyy-MM-dd")
        });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var t = await CurrentTeacherAsync();

        var s = await _db.Subjects
            .FirstOrDefaultAsync(x => x.SubjectId == id && x.TeacherId == t.TeacherId);

        return s == null ? NotFound() : View(s);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var t = await CurrentTeacherAsync();

        var s = await _db.Subjects
            .FirstOrDefaultAsync(x => x.SubjectId == id && x.TeacherId == t.TeacherId);

        if (s != null)
        {
            _db.Subjects.Remove(s);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private void ValidateSchedules(List<SubjectScheduleInputViewModel> schedules)
    {
        schedules.RemoveAll(IsEmptySchedule);

        if (schedules.Count > 3)
            ModelState.AddModelError("", "A subject can only have up to 3 schedule days.");

        var parsedRows = new List<(int Index, string Day, TimeOnly Start, TimeOnly End)>();

        for (var i = 0; i < schedules.Count; i++)
        {
            var row = schedules[i];

            if (string.IsNullOrWhiteSpace(row.Day) || !ScheduleDays.Contains(row.Day))
                ModelState.AddModelError($"Schedules[{i}].Day", "Select a valid day.");

            if (string.IsNullOrWhiteSpace(row.StartTime))
                ModelState.AddModelError($"Schedules[{i}].StartTime", "Start time is required.");

            if (string.IsNullOrWhiteSpace(row.EndTime))
                ModelState.AddModelError($"Schedules[{i}].EndTime", "End time is required.");

            if (ScheduleDays.Contains(row.Day)
                && TimeOnly.TryParse(row.StartTime, out var start)
                && TimeOnly.TryParse(row.EndTime, out var end))
            {
                if (end <= start)
                {
                    AddScheduleError("Schedule end time must be later than start time.");
                }
                else
                {
                    parsedRows.Add((i, row.Day!, start, end));
                }
            }
        }

        for (var i = 0; i < parsedRows.Count; i++)
        {
            for (var j = i + 1; j < parsedRows.Count; j++)
            {
                var first = parsedRows[i];
                var second = parsedRows[j];

                if (first.Day == second.Day && first.Start < second.End && first.End > second.Start)
                {
                    AddScheduleError($"Schedule rows overlap on {first.Day}. Please choose a different time.");
                    i = parsedRows.Count;
                    break;
                }
            }
        }

        while (schedules.Count < 3)
            schedules.Add(new SubjectScheduleInputViewModel());
    }

    private async Task ValidateScheduleConflictsAsync(
        int teacherId,
        int? currentSubjectId,
        List<SubjectScheduleInputViewModel> schedules)
    {
        var rows = schedules.Where(s => !IsEmptySchedule(s)).ToList();

        if (!rows.Any()) return;

        var existing = await _db.SubjectSchedules
            .Include(s => s.Subject)
            .Where(s =>
                s.Subject!.TeacherId == teacherId &&
                (!currentSubjectId.HasValue || s.SubjectId != currentSubjectId.Value))
            .ToListAsync();

        foreach (var row in rows)
        {
            if (!TimeOnly.TryParse(row.StartTime, out var start) ||
                !TimeOnly.TryParse(row.EndTime, out var end))
                continue;

            if (end <= start)
            {
                AddScheduleError("Schedule end time must be later than start time.");
                return;
            }

            var conflict = existing.FirstOrDefault(s =>
                s.Day == row.Day
                && TimeOnly.TryParse(s.StartTime, out var existingStart)
                && TimeOnly.TryParse(s.EndTime, out var existingEnd)
                && start < existingEnd
                && end > existingStart);

            if (conflict != null)
            {
                AddScheduleError(
                    $"{row.Day} {FormatTime(row.StartTime)} - {FormatTime(row.EndTime)} conflicts with {conflict.Subject?.SubjectCode} - {conflict.Subject?.SubjectName} ({FormatTime(conflict.StartTime)} - {FormatTime(conflict.EndTime)}).");

                return;
            }
        }
    }

    private void AddScheduleError(string message)
    {
        ModelState.AddModelError("", message);
        ViewBag.ScheduleError = message;
    }

    private async Task SaveSchedulesAsync(int subjectId, List<SubjectScheduleInputViewModel> schedules)
    {
        var oldRows = await _db.SubjectSchedules
            .Where(s => s.SubjectId == subjectId)
            .ToListAsync();

        _db.SubjectSchedules.RemoveRange(oldRows);

        foreach (var row in schedules.Where(x => !IsEmptySchedule(x)).Take(3))
        {
            _db.SubjectSchedules.Add(new SubjectSchedule
            {
                SubjectId = subjectId,
                Day = row.Day!,
                StartTime = row.StartTime!,
                EndTime = row.EndTime!,
                Room = row.Room
            });
        }

        await _db.SaveChangesAsync();
    }

    private static bool IsEmptySchedule(SubjectScheduleInputViewModel row)
    {
        return string.IsNullOrWhiteSpace(row.Day)
            && string.IsNullOrWhiteSpace(row.StartTime)
            && string.IsNullOrWhiteSpace(row.EndTime)
            && string.IsNullOrWhiteSpace(row.Room);
    }

    private static List<SubjectScheduleInputViewModel> ToScheduleRows(IEnumerable<SubjectSchedule> schedules)
    {
        var rows = schedules
            .OrderBy(s => Array.IndexOf(ScheduleDays, s.Day))
            .ThenBy(s => s.StartTime)
            .Select(s => new SubjectScheduleInputViewModel
            {
                Day = s.Day,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Room = s.Room
            })
            .ToList();

        while (rows.Count < 3)
            rows.Add(new SubjectScheduleInputViewModel());

        return rows.Take(3).ToList();
    }

    private static List<SubjectScheduleInputViewModel> EmptyScheduleRows()
    {
        return new List<SubjectScheduleInputViewModel>
        {
            new(), new(), new()
        };
    }

    // FIXED: Properly converts local time to UTC before stripping time component
    private static DateTime ToAttendanceDate(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : value;
        return DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);
    }

    private static string FormatTime(string? value)
    {
        return TimeOnly.TryParse(value, out var time) ? time.ToString("h:mm tt") : value ?? "";
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

    private static IReadOnlyList<AttendanceDateOptionViewModel> BuildAttendanceDateOptions(IEnumerable<SubjectSchedule> schedules)
    {
        var scheduledDays = schedules
            .Select(s => ToDayOfWeek(s.Day))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .Distinct()
            .ToHashSet();

        if (!scheduledDays.Any())
            return Array.Empty<AttendanceDateOptionViewModel>();

        var start = DateTime.UtcNow.Date.AddDays(-28);
        var end = DateTime.UtcNow.Date.AddDays(84);

        var options = new List<AttendanceDateOptionViewModel>();

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (!scheduledDays.Contains(day.DayOfWeek))
                continue;

            var attendanceDay = ToAttendanceDate(day);

            options.Add(new AttendanceDateOptionViewModel
            {
                Value = attendanceDay.ToString("yyyy-MM-dd"),
                Label = attendanceDay.ToString("dddd, MMM dd, yyyy")
            });
        }

        return options;
    }

        private static DateTime ResolveAttendanceDate(
        DateTime? requestedDate,
        IReadOnlyList<AttendanceDateOptionViewModel> options)
    {
        if (!options.Any())
            return ToAttendanceDate(requestedDate ?? DateTime.UtcNow);

        if (requestedDate.HasValue)
        {
            var requested = ToAttendanceDate(requestedDate.Value);

            if (options.Any(o => o.Value == requested.ToString("yyyy-MM-dd")))
                return requested;
        }

        var today = ToAttendanceDate(DateTime.UtcNow);

        var nextOption = options.FirstOrDefault(o =>
            string.CompareOrdinal(o.Value, today.ToString("yyyy-MM-dd")) >= 0)
            ?? options.Last();

        return ToAttendanceDate(DateTime.Parse(nextOption.Value));
    }

    private static bool IsAllowedAttendanceDate(DateTime date, IEnumerable<SubjectSchedule> schedules)
    {
        var scheduledDays = schedules
            .Select(s => ToDayOfWeek(s.Day))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToHashSet();

        return scheduledDays.Any() && scheduledDays.Contains(date.DayOfWeek);
    }

    private static DayOfWeek? ToDayOfWeek(string day)
    {
        return Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var result)
            ? result
            : null;
    }
}
