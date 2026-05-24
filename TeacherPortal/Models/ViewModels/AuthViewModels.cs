using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models.ViewModels;

public class LoginViewModel
{
    [Required] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class RegisterTeacherViewModel
{
    [Required, StringLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string EmployeeNumber { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegisterStudentViewModel
{
    [Required, StringLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [StringLength(30)] public string? ContactNumber { get; set; }
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
}

public class CreateStudentViewModel
{
    [Required, StringLength(50)] public string StudentNumber { get; set; } = string.Empty;
    [Required, StringLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [StringLength(30)] public string? ContactNumber { get; set; }
    public int? SectionId { get; set; }
    public List<int> SubjectIds { get; set; } = new();
}

public class AddRegisteredStudentViewModel
{
    [Required, StringLength(50)] public string StudentNumber { get; set; } = string.Empty;
    [Required, StringLength(20)] public string EnrollmentCode { get; set; } = string.Empty;
    public int? SectionId { get; set; }
    public List<int> SubjectIds { get; set; } = new();
}

public class DashboardViewModel
{
    public string TeacherName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalSections { get; set; }
    public int AttendanceToday { get; set; }
    public List<Grade> RecentGrades { get; set; } = new();
    public List<Attendance> RecentAttendance { get; set; } = new();
}
