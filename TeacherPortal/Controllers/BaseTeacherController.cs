using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeacherPortal.Data;
using TeacherPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace TeacherPortal.Controllers;

[Authorize(Roles = "Teacher")]
public abstract class BaseTeacherController : Controller
{
    protected readonly ApplicationDbContext _db;
    protected readonly UserManager<ApplicationUser> _users;
    protected BaseTeacherController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    protected async Task<Teacher> CurrentTeacherAsync()
    {
        var uid = _users.GetUserId(User);
        var t = await _db.Teachers.FirstOrDefaultAsync(x => x.UserId == uid);
        if (t == null) throw new InvalidOperationException("Teacher record missing.");
        return t;
    }
}
