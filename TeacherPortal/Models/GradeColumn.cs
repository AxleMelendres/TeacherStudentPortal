namespace TeacherPortal.Models;

public class GradeColumn
{
    public int GradeColumnId { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string Period { get; set; } = "Midterm";
    public string Type { get; set; } = "Activity";
    public string Title { get; set; } = "";
    public decimal MaxScore { get; set; } = 100;
    public decimal Weight { get; set; } = 0;
    public int Order { get; set; } = 0;
    public ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
}
