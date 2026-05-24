using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

public class ScheduleController : BaseTeacherController
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

    public ScheduleController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    public async Task<IActionResult> Index()
    {
        var teacher = await CurrentTeacherAsync();
        var teacherUser = await _users.GetUserAsync(User);
        var subjects = await _db.Subjects
            .Include(s => s.Schedules)
            .Where(s => s.TeacherId == teacher.TeacherId)
            .OrderBy(s => s.SubjectCode)
            .ToListAsync();
        var sections = await _db.Sections
            .Where(s => s.TeacherId == teacher.TeacherId)
            .OrderBy(s => s.SectionName)
            .ToListAsync();

        var entries = new List<ScheduleEntryViewModel>();
        for (var i = 0; i < subjects.Count; i++)
        {
            var subject = subjects[i];
            var section = sections.Count == 0 ? null : sections[i % sections.Count];
            var subjectSchedules = subject.Schedules.Any()
                ? subject.Schedules.OrderBy(s => Array.IndexOf(Days, s.Day)).ThenBy(s => s.StartTime).ToList()
                : new List<SubjectSchedule>
                {
                    new()
                    {
                        Day = Days[i % Days.Length],
                        StartTime = TimeSlots[i % TimeSlots.Length].Split(" - ")[0],
                        EndTime = TimeSlots[i % TimeSlots.Length].Split(" - ")[1],
                        Room = subject.Room
                    }
                };

            foreach (var schedule in subjectSchedules)
            {
                var dayIndex = Array.IndexOf(Days, schedule.Day);
                if (dayIndex < 0) continue;
                entries.Add(new ScheduleEntryViewModel
                {
                    DayOrder = dayIndex,
                    DayName = Days[dayIndex],
                    TimeSlot = FormatTimeSlot(schedule),
                    SubjectCode = subject.SubjectCode,
                    SubjectName = subject.SubjectName,
                    SectionName = section?.SectionName ?? "Unassigned section",
                    Room = string.IsNullOrWhiteSpace(schedule.Room) ? "No room set" : schedule.Room
                });
            }
        }

        var timeSlots = entries.Select(e => e.TimeSlot)
            .Concat(TimeSlots)
            .Distinct()
            .OrderBy(NormalizeTimeSlot)
            .ToList();

        ViewBag.TeacherName = teacherUser?.FullName ?? "Teacher";
        ViewBag.EmployeeNumber = teacher.EmployeeNumber;

        return View(new ScheduleIndexViewModel
        {
            Days = Days,
            TimeSlots = timeSlots,
            Entries = entries
        });
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
