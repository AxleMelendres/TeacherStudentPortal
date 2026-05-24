namespace TeacherPortal.Models;

public class StudentGrade
{
    public int StudentGradeId { get; set; }
    public int GradeColumnId { get; set; }
    public GradeColumn? GradeColumn { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public decimal? Score { get; set; }
}
