namespace TeacherPortal.Models;

public class Grade
{
    public int GradeId { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public decimal? Quiz { get; set; }
    public decimal? Activity { get; set; }
    public decimal? Assignment { get; set; }
    public decimal? MidtermExam { get; set; }
    public decimal? FinalExam { get; set; }
    public decimal? FinalGrade { get; set; }
    public string? Remarks { get; set; }
}