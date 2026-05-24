using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

public class TeacherDashboardController : BaseTeacherController
{
    public TeacherDashboardController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    public async Task<IActionResult> Index()
    {
        var t = await CurrentTeacherAsync();
        var today = DateTime.UtcNow.Date;
        var vm = new DashboardViewModel
        {
            TeacherName = (await _users.GetUserAsync(User))?.FullName ?? "Teacher",
            TotalStudents = await _db.Students.CountAsync(s => s.TeacherId == t.TeacherId),
            TotalSubjects = await _db.Subjects.CountAsync(s => s.TeacherId == t.TeacherId),
            TotalSections = await _db.Sections.CountAsync(s => s.TeacherId == t.TeacherId),
            AttendanceToday = await _db.Attendances.CountAsync(a => a.AttendanceDate == today && a.Student!.TeacherId == t.TeacherId),
            RecentGrades = await _db.Grades.Include(g => g.Student).Include(g => g.Subject)
                .Where(g => g.Student!.TeacherId == t.TeacherId).OrderByDescending(g => g.GradeId).Take(5).ToListAsync(),
            RecentAttendance = await _db.Attendances.Include(a => a.Student).Include(a => a.Subject)
                .Where(a => a.Student!.TeacherId == t.TeacherId).OrderByDescending(a => a.AttendanceId).Take(5).ToListAsync()
        };
        return View(vm);
    }

    public async Task<IActionResult> Profile()
    {
        return View(await BuildProfileViewModelAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(TeacherProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(await BuildProfileViewModelAsync(model));

        var user = await _users.GetUserAsync(User);
        if (user == null)
            return Challenge();

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

        var newEmail = model.Email.Trim();
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _users.SetEmailAsync(user, newEmail);
            if (!emailResult.Succeeded)
            {
                foreach (var error in emailResult.Errors)
                    ModelState.AddModelError(nameof(model.Email), error.Description);
                return View(await BuildProfileViewModelAsync(model));
            }

            var userNameResult = await _users.SetUserNameAsync(user, newEmail);
            if (!userNameResult.Succeeded)
            {
                foreach (var error in userNameResult.Errors)
                    ModelState.AddModelError(nameof(model.Email), error.Description);
                return View(await BuildProfileViewModelAsync(model));
            }
        }

        var updateResult = await _users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(await BuildProfileViewModelAsync(model));
        }

        TempData["Success"] = "Profile settings updated.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<TeacherProfileViewModel> BuildProfileViewModelAsync(TeacherProfileViewModel? posted = null)
    {
        var teacher = await CurrentTeacherAsync();
        var user = await _users.GetUserAsync(User);

        var subjects = await _db.Subjects
            .Where(s => s.TeacherId == teacher.TeacherId)
            .OrderBy(s => s.SubjectCode)
            .Select(s => new TeacherProfileSubjectItem
            {
                Code = s.SubjectCode,
                Name = s.SubjectName,
                Schedule = string.IsNullOrWhiteSpace(s.ScheduleDay)
                    ? null
                    : $"{s.ScheduleDay} {s.ScheduleStartTime}-{s.ScheduleEndTime}",
                Room = s.Room
            })
            .ToListAsync();

        var sections = await _db.Sections
            .Where(s => s.TeacherId == teacher.TeacherId)
            .OrderBy(s => s.YearLevel)
            .ThenBy(s => s.SectionName)
            .Select(s => new TeacherProfileSectionItem
            {
                Name = s.SectionName,
                YearLevel = s.YearLevel,
                StudentCount = s.Students.Count
            })
            .ToListAsync();

        var vm = posted ?? new TeacherProfileViewModel
        {
            FullName = user?.FullName ?? string.Empty,
            Email = user?.Email ?? string.Empty,
            PhoneNumber = user?.PhoneNumber
        };

        vm.EmployeeNumber = teacher.EmployeeNumber;
        vm.Role = user?.Role ?? "Teacher";
        vm.EmailConfirmed = user?.EmailConfirmed ?? false;
        vm.TwoFactorEnabled = user?.TwoFactorEnabled ?? false;
        vm.UserName = user?.UserName;
        vm.TotalStudents = await _db.Students.CountAsync(s => s.TeacherId == teacher.TeacherId);
        vm.TotalSubjects = subjects.Count;
        vm.TotalSections = sections.Count;
        vm.TotalSchedules = await _db.SubjectSchedules.CountAsync(s => s.Subject!.TeacherId == teacher.TeacherId);
        vm.Subjects = subjects.Take(6).ToList();
        vm.Sections = sections.Take(6).ToList();

        return vm;
    }
}
