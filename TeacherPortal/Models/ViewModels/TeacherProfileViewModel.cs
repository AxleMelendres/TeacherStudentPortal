using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models.ViewModels;

public class TeacherProfileViewModel
{
    [Required, StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "Teacher";
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? UserName { get; set; }

    public int TotalStudents { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalSections { get; set; }
    public int TotalSchedules { get; set; }

    public List<TeacherProfileSubjectItem> Subjects { get; set; } = new();
    public List<TeacherProfileSectionItem> Sections { get; set; } = new();
}

public class TeacherProfileSubjectItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Schedule { get; set; }
    public string? Room { get; set; }
}

public class TeacherProfileSectionItem
{
    public string Name { get; set; } = string.Empty;
    public string YearLevel { get; set; } = string.Empty;
    public int StudentCount { get; set; }
}
