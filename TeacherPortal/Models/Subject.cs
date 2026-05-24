using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models;

public class Subject
{
    public int SubjectId { get; set; }
    public int TeacherId { get; set; }
    [Required, StringLength(20)] public string SubjectCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string SubjectName { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [StringLength(20)] public string? ScheduleDay { get; set; }
    [StringLength(20)] public string? ScheduleStartTime { get; set; }
    [StringLength(20)] public string? ScheduleEndTime { get; set; }
    [StringLength(50)] public string? Room { get; set; }
    public Teacher? Teacher { get; set; }
    public ICollection<SubjectSchedule> Schedules { get; set; } = new List<SubjectSchedule>();
}
