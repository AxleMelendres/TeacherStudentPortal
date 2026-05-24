using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeacherPortal.Data;
using TeacherPortal.Models;
using TeacherPortal.Models.ViewModels;

namespace TeacherPortal.Controllers;

public class SectionsController : BaseTeacherController
{
    public SectionsController(ApplicationDbContext db, UserManager<ApplicationUser> u) : base(db, u) {}

    public async Task<IActionResult> Index()
    {
        var t = await CurrentTeacherAsync();
        var sections = await _db.Sections
            .Where(s => s.TeacherId == t.TeacherId)
            .OrderBy(s => s.SectionName)
            .Select(s => new SectionIndexViewModel
            {
                SectionId = s.SectionId,
                SectionName = s.SectionName,
                YearLevel = s.YearLevel,
                Description = s.Description,
                StudentCount = s.Students.Count
            })
            .ToListAsync();

        return View(sections);
    }

    public IActionResult Create() => View(new Section());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Section m)
    {
        if (!ModelState.IsValid) return View(m);
        var t = await CurrentTeacherAsync();
        m.TeacherId = t.TeacherId;
        _db.Sections.Add(m); await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Sections.FirstOrDefaultAsync(x => x.SectionId == id && x.TeacherId == t.TeacherId);
        if (s == null) return NotFound();
        return View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Section input)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Sections.FirstOrDefaultAsync(x => x.SectionId == id && x.TeacherId == t.TeacherId);
        if (s == null) return NotFound();
        s.SectionName = input.SectionName; s.YearLevel = input.YearLevel; s.Description = input.Description;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Sections
            .Include(x => x.Students)
            .FirstOrDefaultAsync(x => x.SectionId == id && x.TeacherId == t.TeacherId);

        if (s == null) return NotFound();

        var studentIds = s.Students.Select(st => st.StudentId).ToList();
        var subjectRows = await _db.StudentSubjects
            .Include(ss => ss.Subject)
            .Where(ss => studentIds.Contains(ss.StudentId) && ss.Subject!.TeacherId == t.TeacherId)
            .ToListAsync();

        var subjectsByStudent = subjectRows
            .GroupBy(ss => ss.StudentId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Where(ss => ss.Subject != null)
                    .Select(ss => $"{ss.Subject!.SubjectCode} - {ss.Subject.SubjectName}")
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList());

        var vm = new SectionDetailsViewModel
        {
            SectionId = s.SectionId,
            SectionName = s.SectionName,
            YearLevel = s.YearLevel,
            Description = s.Description,
            StudentCount = s.Students.Count,
            EnrolledSubjectCount = subjectRows.Select(ss => ss.SubjectId).Distinct().Count(),
            Students = s.Students
                .OrderBy(st => st.FullName)
                .Select(st => new SectionStudentRosterItem
                {
                    StudentId = st.StudentId,
                    StudentNumber = st.StudentNumber,
                    FullName = st.FullName,
                    Email = st.Email,
                    ContactNumber = st.ContactNumber,
                    Subjects = subjectsByStudent.TryGetValue(st.StudentId, out var subjects) ? subjects : new List<string>()
                })
                .ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Sections.FirstOrDefaultAsync(x => x.SectionId == id && x.TeacherId == t.TeacherId);
        return s == null ? NotFound() : View(s);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var t = await CurrentTeacherAsync();
        var s = await _db.Sections.FirstOrDefaultAsync(x => x.SectionId == id && x.TeacherId == t.TeacherId);
        if (s != null) { _db.Sections.Remove(s); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
