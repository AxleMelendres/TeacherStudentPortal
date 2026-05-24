using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signin;
    private readonly ApplicationDbContext _db;
    public AccountController(UserManager<ApplicationUser> u, SignInManager<ApplicationUser> s, ApplicationDbContext db)
    { _users = u; _signin = s; _db = db; }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _users.GetUserAsync(User);
            if (user != null) return await RedirectByRoleAsync(user);
        }

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var login = m.Email.Trim();
        var user = await _users.FindByEmailAsync(login);

        if (user == null) { ModelState.AddModelError("", "Invalid credentials"); return View(m); }
        if (!await _users.IsInRoleAsync(user, DbSeeder.TeacherRole))
        {
            ModelState.AddModelError("", "Use the student portal to sign in with this account.");
            return View(m);
        }

        var res = await _signin.PasswordSignInAsync(user, m.Password, m.RememberMe, false);
        if (!res.Succeeded) { ModelState.AddModelError("", "Invalid credentials"); return View(m); }
        return RedirectToAction("Index", "TeacherDashboard");
    }

    [HttpGet] public IActionResult RegisterTeacher() => View();

    [HttpGet]
    public async Task<IActionResult> StudentLogin()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _users.GetUserAsync(User);
            if (user != null) return await RedirectByRoleAsync(user);
        }

        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StudentLogin(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var login = m.Email.Trim();
        var user = await _users.FindByEmailAsync(login);
        if (user == null)
        {
            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentNumber == login);
            user = student?.User;
        }

        if (user == null) { ModelState.AddModelError("", "Invalid credentials"); return View(m); }
        if (!await _users.IsInRoleAsync(user, DbSeeder.StudentRole))
        {
            ModelState.AddModelError("", "Use the teacher portal to sign in with this account.");
            return View(m);
        }

        var res = await _signin.PasswordSignInAsync(user, m.Password, m.RememberMe, false);
        if (!res.Succeeded) { ModelState.AddModelError("", "Invalid credentials"); return View(m); }
        return RedirectToAction("Index", "StudentDashboard");
    }

    [HttpGet] public IActionResult RegisterStudent() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterStudent(RegisterStudentViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var email = m.Email.Trim();
        var fullName = m.FullName.Trim();
        await using var tx = await _db.Database.BeginTransactionAsync();

        var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName, Role = DbSeeder.StudentRole, EmailConfirmed = true };
        var res = await _users.CreateAsync(user, m.Password);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }

        await _users.AddToRoleAsync(user, DbSeeder.StudentRole);

        var studentNumber = await GenerateStudentNumberAsync();
        var enrollmentCode = GenerateCode(8);
        _db.Students.Add(new Student
        {
            UserId = user.Id,
            StudentNumber = studentNumber,
            FullName = fullName,
            Email = email,
            ContactNumber = m.ContactNumber,
            EnrollmentCode = enrollmentCode,
            TeacherId = null,
            SectionId = null
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["msg"] = $"Student account created. Student ID: {studentNumber}. Enrollment code: {enrollmentCode}. Give both to your teacher.";
        return RedirectToAction(nameof(StudentLogin));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterTeacher(RegisterTeacherViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var user = new ApplicationUser { UserName = m.Email, Email = m.Email, FullName = m.FullName, Role = "Teacher", EmailConfirmed = true };
        var res = await _users.CreateAsync(user, m.Password);
        if (!res.Succeeded) { foreach (var e in res.Errors) ModelState.AddModelError("", e.Description); return View(m); }
        await _users.AddToRoleAsync(user, "Teacher");
        _db.Teachers.Add(new Teacher { UserId = user.Id, EmployeeNumber = m.EmployeeNumber });
        await _db.SaveChangesAsync();
        await _signin.SignInAsync(user, false);
        return RedirectToAction("Index", "TeacherDashboard");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var user = await _users.GetUserAsync(User);
        var isStudent = user != null && await _users.IsInRoleAsync(user, DbSeeder.StudentRole);
        await _signin.SignOutAsync();
        TempData["msg"] = "You have signed out securely.";
        return RedirectToAction(isStudent ? nameof(StudentLogin) : nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    private async Task<IActionResult> RedirectByRoleAsync(ApplicationUser user)
    {
        if (await _users.IsInRoleAsync(user, "Teacher"))
            return RedirectToAction("Index", "TeacherDashboard");

        if (await _users.IsInRoleAsync(user, "Student"))
            return RedirectToAction("Index", "StudentDashboard");

        await _signin.SignOutAsync();
        return RedirectToAction(nameof(AccessDenied));
    }

    private async Task<string> GenerateStudentNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        string studentNumber;

        do
        {
            studentNumber = $"STU-{year}-{GenerateCode(6)}";
        }
        while (await _db.Students.AnyAsync(s => s.StudentNumber == studentNumber));

        return studentNumber;
    }

    private static string GenerateCode(int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[length];
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);

        for (var i = 0; i < length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];

        return new string(chars);
    }
}
