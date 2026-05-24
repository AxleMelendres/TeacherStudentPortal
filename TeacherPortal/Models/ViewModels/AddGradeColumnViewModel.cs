using System.ComponentModel.DataAnnotations;

namespace TeacherPortal.Models.ViewModels;

public class AddGradeColumnViewModel
{
    [Required] public string Period { get; set; } = "Midterm";
    [Required] public string Type { get; set; } = "Activity";
    [Required] public string Title { get; set; } = "";
    [Range(1, 1000)] public decimal MaxScore { get; set; } = 100;
}