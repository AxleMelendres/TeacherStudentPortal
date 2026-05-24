namespace TeacherPortal.Models.ViewModels;

public class GradeIndexViewModel
{
    public Subject Subject { get; set; } = null!;
    public List<Student> Students { get; set; } = new();
    public List<GradeColumn> Columns { get; set; } = new();
    public Dictionary<int, Dictionary<int, decimal?>> Scores { get; set; } = new();
    // Scores[studentId][columnId] = score
    public GradeWeight MidtermWeight { get; set; } = new();
    public GradeWeight FinalsWeight { get; set; } = new();
    public Dictionary<int, decimal?> MidtermGrades { get; set; } = new();
    public Dictionary<int, decimal?> FinalsGrades { get; set; } = new();
    public Dictionary<int, decimal?> FinalGrades { get; set; } = new();
}