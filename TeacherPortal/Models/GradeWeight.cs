namespace TeacherPortal.Models;

public class GradeWeight
{
    public int GradeWeightId { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public string Period { get; set; } = "Midterm"; // Midterm | Finals
    public decimal ActivityWeight { get; set; } = 25;
    public decimal QuizWeight { get; set; } = 25;
    public decimal ExamWeight { get; set; } = 25;
    public decimal ProjectWeight { get; set; } = 25;

    public decimal MidtermWeight { get; set; } = 50; // only used on Midterm row
    public decimal FinalsWeight { get; set; } = 50;  // only used on Midterm row
}