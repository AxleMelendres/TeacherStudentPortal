using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

public class StudentsController : BaseTeacherController
{
    public StudentsController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    public async Task<IActionResult> Index(string? q, int? sectionId, int? subjectId)
    {
        var t = await CurrentTeacherAsync();
        var query = _db.Students.Include(s => s.Section).Where(s => s.TeacherId == t.TeacherId);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(s => s.FullName.Contains(q) || s.StudentNumber.Contains(q) || s.Email.Contains(q));
        if (sectionId.HasValue) query = query.Where(s => s.SectionId == sectionId);
        if (subjectId.HasValue)
            query = query.Where(s => _db.StudentSubjects.Any(ss => ss.StudentId == s.StudentId && ss.SubjectId == subjectId));
        ViewBag.Sections = new SelectList(await _db.Sections.Where(s => s.TeacherId == t.TeacherId).ToListAsync(), "SectionId", "SectionName", sectionId);
        ViewBag.Subjects = new SelectList(await _db.Subjects.Where(s => s.TeacherId == t.TeacherId).ToListAsync(), "SubjectId", "SubjectName", subjectId);
        ViewBag.Query = q;
        return View(await query.OrderBy(s => s.FullName).ToListAsync());
    }

    public async Task<IActionResult> Create(int? subjectId)
    {
        var t = await CurrentTeacherAsync();
        ViewBag.Sections = new SelectList(await _db.Sections.Where(s => s.TeacherId == t.TeacherId).ToListAsync(), "SectionId", "SectionName");
        ViewBag.Subjects = await _db.Subjects.Where(s => s.TeacherId == t.TeacherId).ToListAsync();
        return View(new CreateStudentViewModel
        {
            SubjectIds = subjectId.HasValue ? new List<int> { subjectId.Value } : new List<int>()
        });
    }

    public async Task<IActionResult> AddRegistered()
    {
        var t = await CurrentTeacherAsync();
        await PopulateRegisteredStudentListsAsync(t.TeacherId, null);
        return View(new AddRegisteredStudentViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRegistered(AddRegisteredStudentViewModel m)
    {
        var t = await CurrentTeacherAsync();
        var teacherSubjectIds = await _db.Subjects
            .Where(s => s.TeacherId == t.TeacherId)
            .Select(s => s.SubjectId)
            .ToListAsync();

        m.StudentNumber = m.StudentNumber.Trim();
        m.EnrollmentCode = m.EnrollmentCode.Trim().ToUpperInvariant();
        m.SubjectIds = m.SubjectIds.Distinct().Where(teacherSubjectIds.Contains).ToList();

        var student = await _db.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentNumber == m.StudentNumber);

        if (student == null)
            ModelState.AddModelError(nameof(m.StudentNumber), "No registered student was found with this Student ID.");
        else if (student.TeacherId.HasValue && student.TeacherId.Value != t.TeacherId)
            ModelState.AddModelError(nameof(m.StudentNumber), "This student is already connected to another teacher.");
        else if (string.IsNullOrWhiteSpace(student.EnrollmentCode) ||
                 !string.Equals(student.EnrollmentCode, m.EnrollmentCode, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(m.EnrollmentCode), "The enrollment code does not match this student.");

        if (m.SectionId.HasValue && !await IsTeacherSectionAsync(t.TeacherId, m.SectionId.Value))
            ModelState.AddModelError(nameof(m.SectionId), "Select one of your own sections.");

        if (!ModelState.IsValid || student == null)
        {
            await PopulateRegisteredStudentListsAsync(t.TeacherId, m.SectionId);
            return View(m);
        }

        student.TeacherId = t.TeacherId;
        student.SectionId = m.SectionId;

        var existingEnrollments = await _db.StudentSubjects
            .Where(ss => ss.StudentId == student.StudentId && teacherSubjectIds.Contains(ss.SubjectId))
            .ToListAsync();

        foreach (var enrollment in existingEnrollments.Where(ss => !m.SubjectIds.Contains(ss.SubjectId)))
            _db.StudentSubjects.Remove(enrollment);

        var existingSubjectIds = existingEnrollments.Select(ss => ss.SubjectId).ToHashSet();
        foreach (var subjectId in m.SubjectIds.Where(subjectId => !existingSubjectIds.Contains(subjectId)))
            _db.StudentSubjects.Add(new StudentSubject { StudentId = student.StudentId, SubjectId = subjectId, SectionId = m.SectionId });

        foreach (var enrollment in existingEnrollments.Where(ss => m.SubjectIds.Contains(ss.SubjectId)))
            enrollment.SectionId = m.SectionId;

        await _db.SaveChangesAsync();
        TempData["msg"] = $"{student.FullName} has been added to your class records.";
        return RedirectToAction(nameof(Details), new { id = student.StudentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStudentViewModel m)
    {
        var t = await CurrentTeacherAsync();
        var teacherSubjectIds = await _db.Subjects
            .Where(s => s.TeacherId == t.TeacherId)
            .Select(s => s.SubjectId)
            .ToListAsync();

        m.SubjectIds = m.SubjectIds
            .Distinct()
            .Where(teacherSubjectIds.Contains)
            .ToList();

        if (m.SectionId.HasValue && !await IsTeacherSectionAsync(t.TeacherId, m.SectionId.Value))
            ModelState.AddModelError(nameof(m.SectionId), "Select one of your own sections.");

        if (!ModelState.IsValid)
        {
            ViewBag.Sections = new SelectList(await _db.Sections.Where(s => s.TeacherId == t.TeacherId).ToListAsync(), "SectionId", "SectionName");
            ViewBag.Subjects = await _db.Subjects.Where(s => s.TeacherId == t.TeacherId).ToListAsync();
            return View(m);
        }
        var user = new ApplicationUser { UserName = m.Email, Email = m.Email, FullName = m.FullName, Role = "Student", EmailConfirmed = true };
        var temporaryPassword = GenerateTemporaryPassword();
        var res = await _users.CreateAsync(user, temporaryPassword);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            ViewBag.Sections = new SelectList(await _db.Sections.Where(s => s.TeacherId == t.TeacherId).ToListAsync(), "SectionId", "SectionName");
            ViewBag.Subjects = await _db.Subjects.Where(s => s.TeacherId == t.TeacherId).ToListAsync();
            return View(m);
        }
        await _users.AddToRoleAsync(user, "Student");
        var student = new Student { UserId = user.Id, StudentNumber = m.StudentNumber, FullName = m.FullName, Email = m.Email, ContactNumber = m.ContactNumber, SectionId = m.SectionId, TeacherId = t.TeacherId };
        _db.Students.Add(student);
        await _db.SaveChangesAsync();
        foreach (var sid in m.SubjectIds)
            _db.StudentSubjects.Add(new StudentSubject { StudentId = student.StudentId, SubjectId = sid, SectionId = m.SectionId });
        await _db.SaveChangesAsync();
        TempData["msg"] = $"Student created. Temporary password: {temporaryPassword}";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Students.Include(x => x.Section).FirstOrDefaultAsync(x => x.StudentId == id && x.TeacherId == t.TeacherId);
        if (s == null) return NotFound();
        ViewBag.Subjects = await _db.StudentSubjects.Include(x => x.Subject).Where(x => x.StudentId == id).ToListAsync();
        return View(s);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Students.FirstOrDefaultAsync(x => x.StudentId == id && x.TeacherId == t.TeacherId);
        if (s == null) return NotFound();
        ViewBag.Sections = new SelectList(await _db.Sections.Where(x => x.TeacherId == t.TeacherId).ToListAsync(), "SectionId", "SectionName", s.SectionId);
        ViewBag.Subjects = await _db.Subjects.Where(x => x.TeacherId == t.TeacherId).OrderBy(x => x.SubjectCode).ToListAsync();
        ViewBag.SelectedSubjectIds = await _db.StudentSubjects
            .Where(ss => ss.StudentId == s.StudentId && ss.Subject!.TeacherId == t.TeacherId)
            .Select(ss => ss.SubjectId)
            .ToListAsync();
        return View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Student input, List<int> subjectIds)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Students.FirstOrDefaultAsync(x => x.StudentId == id && x.TeacherId == t.TeacherId);
        if (s == null) return NotFound();

        if (input.SectionId.HasValue && !await IsTeacherSectionAsync(t.TeacherId, input.SectionId.Value))
        {
            ModelState.AddModelError(nameof(input.SectionId), "Select one of your own sections.");
            await PopulateStudentEditListsAsync(t.TeacherId, input.SectionId, s.StudentId);
            return View(input);
        }

        s.FullName = input.FullName; s.StudentNumber = input.StudentNumber; s.Email = input.Email;
        s.ContactNumber = input.ContactNumber; s.SectionId = input.SectionId;
        var user = string.IsNullOrWhiteSpace(s.UserId) ? null : await _users.FindByIdAsync(s.UserId);
        if (user != null)
        {
            user.FullName = input.FullName;
            user.PhoneNumber = input.ContactNumber;
            if (!string.Equals(user.Email, input.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailResult = await _users.SetEmailAsync(user, input.Email);
                if (!emailResult.Succeeded)
                {
                    foreach (var error in emailResult.Errors)
                        ModelState.AddModelError(nameof(input.Email), error.Description);
                    await PopulateStudentEditListsAsync(t.TeacherId, input.SectionId, s.StudentId);
                    return View(input);
                }

                var userNameResult = await _users.SetUserNameAsync(user, input.Email);
                if (!userNameResult.Succeeded)
                {
                    foreach (var error in userNameResult.Errors)
                        ModelState.AddModelError(nameof(input.Email), error.Description);
                    await PopulateStudentEditListsAsync(t.TeacherId, input.SectionId, s.StudentId);
                    return View(input);
                }
            }

            var updateResult = await _users.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                await PopulateStudentEditListsAsync(t.TeacherId, input.SectionId, s.StudentId);
                return View(input);
            }
        }

        var teacherSubjectIds = await _db.Subjects
            .Where(subject => subject.TeacherId == t.TeacherId)
            .Select(subject => subject.SubjectId)
            .ToListAsync();
        var selectedSubjectIds = subjectIds.Distinct().Where(teacherSubjectIds.Contains).ToList();
        var enrollments = await _db.StudentSubjects.Where(ss => ss.StudentId == s.StudentId).ToListAsync();
        var teacherEnrollments = enrollments.Where(ss => teacherSubjectIds.Contains(ss.SubjectId)).ToList();

        foreach (var enrollment in teacherEnrollments.Where(ss => !selectedSubjectIds.Contains(ss.SubjectId)))
            _db.StudentSubjects.Remove(enrollment);

        var existingSubjectIds = teacherEnrollments.Select(ss => ss.SubjectId).ToHashSet();
        foreach (var subjectId in selectedSubjectIds.Where(subjectId => !existingSubjectIds.Contains(subjectId)))
            _db.StudentSubjects.Add(new StudentSubject { StudentId = s.StudentId, SubjectId = subjectId, SectionId = input.SectionId });

        foreach (var enrollment in teacherEnrollments.Where(ss => selectedSubjectIds.Contains(ss.SubjectId)))
            enrollment.SectionId = input.SectionId;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Students.FirstOrDefaultAsync(x => x.StudentId == id && x.TeacherId == t.TeacherId);
        if (s == null) return NotFound();
        return View(s);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Students.FirstOrDefaultAsync(x => x.StudentId == id && x.TeacherId == t.TeacherId);
        if (s != null)
        {
            var userId = s.UserId;
            _db.Students.Remove(s);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var user = await _users.FindByIdAsync(userId);
                if (user != null)
                    await _users.DeleteAsync(user);
            }
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateStudentEditListsAsync(int teacherId, int? sectionId, int studentId)
    {
        ViewBag.Sections = new SelectList(await _db.Sections.Where(x => x.TeacherId == teacherId).ToListAsync(), "SectionId", "SectionName", sectionId);
        ViewBag.Subjects = await _db.Subjects.Where(x => x.TeacherId == teacherId).OrderBy(x => x.SubjectCode).ToListAsync();
        ViewBag.SelectedSubjectIds = await _db.StudentSubjects
            .Where(ss => ss.StudentId == studentId && ss.Subject!.TeacherId == teacherId)
            .Select(ss => ss.SubjectId)
            .ToListAsync();
    }

    private async Task PopulateRegisteredStudentListsAsync(int teacherId, int? sectionId)
    {
        ViewBag.Sections = new SelectList(await _db.Sections.Where(x => x.TeacherId == teacherId).ToListAsync(), "SectionId", "SectionName", sectionId);
        ViewBag.Subjects = await _db.Subjects.Where(x => x.TeacherId == teacherId).OrderBy(x => x.SubjectCode).ToListAsync();
    }

    private async Task<bool> IsTeacherSectionAsync(int teacherId, int sectionId)
    {
        return await _db.Sections.AnyAsync(s => s.TeacherId == teacherId && s.SectionId == sectionId);
    }

    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        Span<char> chars = stackalloc char[10];
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);

        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];

        return $"S!{new string(chars)}";
    }
}
