namespace TeacherPortal.Models.ViewModels;

public class StudentPortalGradeViewModel
{
    public int SubjectId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string SectionName { get; set; } = "No section";
    public decimal? MidtermGrade { get; set; }
    public decimal? FinalsGrade { get; set; }
    public decimal? FinalGrade { get; set; }
    public string Remarks { get; set; } = "In Progress";
    public int RecordedScores { get; set; }
    public int TotalColumns { get; set; }
    public bool UsesTeacherGradebook { get; set; }
    public List<StudentPortalScoreItemViewModel> Scores { get; set; } = new();
}

public class StudentPortalScoreItemViewModel
{
    public string Period { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal? Score { get; set; }
    public int Order { get; set; }
}
