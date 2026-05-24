using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models;

public class Student
{
    public int StudentId { get; set; }
    public string? UserId { get; set; }
    [Required, StringLength(50)] public string StudentNumber { get; set; } = string.Empty;
    [Required, StringLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = string.Empty;
    [StringLength(30)] public string? ContactNumber { get; set; }
    [StringLength(20)] public string? EnrollmentCode { get; set; }
    public int? SectionId { get; set; }
    public int? TeacherId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Section? Section { get; set; }
    public Teacher? Teacher { get; set; }
    public ApplicationUser? User { get; set; }
}
