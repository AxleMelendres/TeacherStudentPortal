namespace TeacherPortal.Models;

public class StudentSubject
{
    public int StudentSubjectId { get; set; }
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public int? SectionId { get; set; }
    public Student? Student { get; set; }
    public Subject? Subject { get; set; }
    public Section? Section { get; set; }
}
