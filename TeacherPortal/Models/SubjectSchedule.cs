using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models;

public class SubjectSchedule
{
    public int SubjectScheduleId { get; set; }
    public int SubjectId { get; set; }
    [Required, StringLength(20)] public string Day { get; set; } = string.Empty;
    [Required, StringLength(20)] public string StartTime { get; set; } = string.Empty;
    [Required, StringLength(20)] public string EndTime { get; set; } = string.Empty;
    [StringLength(50)] public string? Room { get; set; }
    public Subject? Subject { get; set; }
}
