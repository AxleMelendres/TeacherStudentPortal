using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models;

public class Attendance
{
    public int AttendanceId { get; set; }

    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int? SectionId { get; set; }

    [Required]
    public DateTime AttendanceDate { get; set; } = DateTime.UtcNow.Date;

    [Required, StringLength(20)]
    public string Status { get; set; } = "Present";

    public bool HasExcuseLetter { get; set; }

    [StringLength(300)]
    public string? Remarks { get; set; }

    public Student? Student { get; set; }
    public Subject? Subject { get; set; }
    public Section? Section { get; set; }
}