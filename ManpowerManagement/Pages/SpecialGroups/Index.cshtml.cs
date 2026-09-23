using ManpowerManagement.Data;
using ManpowerManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ManpowerManagement.Pages.SpecialGroups;

public class IndexModel(AppDbContext db) : PageModel
{
    public class GroupRow { public int Id { get; set; } public string Name { get; set; } = ""; public string ColorHex { get; set; } = "#6c757d"; public bool IsActive { get; set; } public int EmployeeCount { get; set; } }
    public class GroupInput { public int? Id { get; set; } public string Name { get; set; } = ""; public string ColorHex { get; set; } = "#6c757d"; public bool IsActive { get; set; } = true; }

    [BindProperty] public GroupInput Input { get; set; } = new();
    public List<GroupRow> Groups { get; set; } = [];
    [TempData] public string? Message { get; set; }
    public string? Error { get; set; }

    public async Task OnGetAsync(int? editId)
    {
        await Load();
        if (editId is int id && await db.SpecialGroups.FindAsync(id) is SpecialGroup g)
            Input = new GroupInput { Id = g.Id, Name = g.Name, ColorHex = g.ColorHex, IsActive = g.IsActive };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name)) return await ShowError("Name is required.");
        var name = Input.Name.Trim();
        if (await db.SpecialGroups.AnyAsync(x => x.Name == name && x.Id != Input.Id)) return await ShowError($"Special group '{name}' already exists.");
        var group = Input.Id is int id ? await db.SpecialGroups.FindAsync(id) : null;
        if (group is null) { group = new SpecialGroup(); db.SpecialGroups.Add(group); }
        group.Name = name;
        group.ColorHex = string.IsNullOrWhiteSpace(Input.ColorHex) ? "#6c757d" : Input.ColorHex;
        group.IsActive = Input.IsActive;
        await db.SaveChangesAsync();
        Message = "Special group saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (await db.EmployeeSpecialGroups.AnyAsync(x => x.SpecialGroupId == id))
            return await ShowError("Cannot delete this group because employees are still assigned to it. Deactivate it instead.");
        var group = await db.SpecialGroups.FindAsync(id);
        if (group is null) return RedirectToPage();
        db.SpecialGroups.Remove(group);
        await db.SaveChangesAsync();
        Message = "Special group deleted.";
        return RedirectToPage();
    }

    private async Task<IActionResult> ShowError(string error) { Error = error; await Load(); return Page(); }

    private async Task Load()
    {
        Groups = await db.SpecialGroups
            .OrderBy(x => x.Name)
            .Select(x => new GroupRow { Id = x.Id, Name = x.Name, ColorHex = x.ColorHex, IsActive = x.IsActive, EmployeeCount = db.EmployeeSpecialGroups.Count(y => y.SpecialGroupId == x.Id) })
            .ToListAsync();
    }
}
