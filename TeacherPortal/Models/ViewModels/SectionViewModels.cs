namespace TeacherPortal.Models.ViewModels;

public class SectionIndexViewModel
{
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string YearLevel { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StudentCount { get; set; }
}

public class SectionDetailsViewModel
{
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string YearLevel { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StudentCount { get; set; }
    public int EnrolledSubjectCount { get; set; }
    public List<SectionStudentRosterItem> Students { get; set; } = new();
}

public class SectionStudentRosterItem
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public List<string> Subjects { get; set; } = new();
}
