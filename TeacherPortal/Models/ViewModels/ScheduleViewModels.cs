using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models.ViewModels;

public class ScheduleEntryViewModel
{
    public int DayOrder { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string TimeSlot { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
}

public class SubjectStudentAttendanceViewModel
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string Status { get; set; } = "Not marked";
    public bool HasExcuseLetter { get; set; }
    public int? AttendanceId { get; set; }
}

public class SubjectDetailsViewModel
{
    public Subject Subject { get; set; } = new();
    public DateTime AttendanceDate { get; set; }
    public IReadOnlyList<AttendanceDateOptionViewModel> AttendanceDateOptions { get; set; } = Array.Empty<AttendanceDateOptionViewModel>();
    public IReadOnlyList<SubjectStudentAttendanceViewModel> Students { get; set; } = Array.Empty<SubjectStudentAttendanceViewModel>();
}

public class AttendanceDateOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class ScheduleIndexViewModel
{
    public IReadOnlyList<string> Days { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TimeSlots { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ScheduleEntryViewModel> Entries { get; set; } = Array.Empty<ScheduleEntryViewModel>();
}

public class SubjectScheduleInputViewModel
{
    public string? Day { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Room { get; set; }
}

public class SubjectFormViewModel
{
    public int SubjectId { get; set; }
    [Required, StringLength(20)]
    public string SubjectCode { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string SubjectName { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
    public List<SubjectScheduleInputViewModel> Schedules { get; set; } = new();
}
