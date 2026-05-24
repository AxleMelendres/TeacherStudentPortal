using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models;

public class Section
{
    public int SectionId { get; set; }
    public int TeacherId { get; set; }
    [Required, StringLength(100)] public string SectionName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string YearLevel { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    public Teacher? Teacher { get; set; }
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
