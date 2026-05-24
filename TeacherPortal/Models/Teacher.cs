using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models;

public class Teacher
{
    public int TeacherId { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string EmployeeNumber { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}
