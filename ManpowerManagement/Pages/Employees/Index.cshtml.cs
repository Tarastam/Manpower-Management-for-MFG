using ManpowerManagement.Data;
using ManpowerManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ManpowerManagement.Pages.Employees;

public class IndexModel(AppDbContext db) : PageModel
{
    public class EmployeeInput { public int? Id { get; set; } public string EmployeeId { get; set; } = ""; public string FullName { get; set; } = ""; public string JobGrade { get; set; } = ""; public string Process { get; set; } = ""; public int WorkshopId { get; set; } public string Shift { get; set; } = "Day"; public string Accommodation { get; set; } = ""; public string TelephoneNumber { get; set; } = ""; public EmploymentState EmploymentState { get; set; } = EmploymentState.Active; public string Remark { get; set; } = ""; public List<int> GroupIds { get; set; } = []; }
    public class BatchEmployeeInput { public string EmployeeId { get; set; } = ""; public string FirstName { get; set; } = ""; public string LastName { get; set; } = ""; public string JobGrade { get; set; } = ""; public string Process { get; set; } = ""; public string Accommodation { get; set; } = ""; public string TelephoneNumber { get; set; } = ""; public string Status { get; set; } = "Active"; public string Remark { get; set; } = ""; public string Groups { get; set; } = ""; }
    public class GridEmployeeInput { public int Id { get; set; } public string EmployeeId { get; set; } = ""; public string FullName { get; set; } = ""; public string JobGrade { get; set; } = ""; public string Process { get; set; } = ""; public int WorkshopId { get; set; } public string Shift { get; set; } = "Day"; public string Accommodation { get; set; } = ""; public string TelephoneNumber { get; set; } = ""; public string EmploymentState { get; set; } = "Active"; public string Remark { get; set; } = ""; public List<string> Groups { get; set; } = []; }
    [BindProperty] public EmployeeInput Input { get; set; } = new();
    [BindProperty] public List<BatchEmployeeInput> BatchRows { get; set; } = [];
    [BindProperty] public string BatchWorkshop { get; set; } = "";
    [BindProperty] public string BatchShift { get; set; } = "";
    [BindProperty] public List<GridEmployeeInput> GridRows { get; set; } = [];
    public List<Employee> Employees { get; set; } = [];
    public List<SelectListItem> WorkshopOptions { get; set; } = [];
    public List<SelectListItem> GroupOptions { get; set; } = [];
    [TempData] public string? Message { get; set; }
    public string? Error { get; set; }

    public async Task OnGetAsync(int? editId) { await Load(); AddBlankBatchRows(); if (editId is int id && Employees.FirstOrDefault(x => x.Id == id) is Employee e) { Input = FromEmployee(e); if (e.CurrentWorkshop is Workshop current && WorkshopOptions.All(x => x.Value != current.Id.ToString())) WorkshopOptions.Add(new SelectListItem(current.Name, current.Id.ToString())); } }
    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.EmployeeId) || string.IsNullOrWhiteSpace(Input.FullName) || Input.WorkshopId == 0) return await ShowError("Emp ID, Name and Workshop are required.");
        if (!await db.Workshops.AnyAsync(x => x.Id == Input.WorkshopId)) return await ShowError("Select a valid Workshop.");
        if (Input.Shift is not ("A" or "B" or "C" or "Day")) return await ShowError("Select a valid Shift.");
        var employee = Input.Id is int id ? await db.Employees.FindAsync(id) : null;
        if (await db.Employees.AnyAsync(x => x.EmployeeId == Input.EmployeeId && x.Id != Input.Id)) return await ShowError($"Emp ID '{Input.EmployeeId}' already exists.");
        if (employee is null) { employee = new Employee { EmployeeId = Input.EmployeeId, StartDate = DateOnly.FromDateTime(DateTime.Today) }; db.Employees.Add(employee); }
        else employee.EmployeeId = Input.EmployeeId;
        Apply(Input, employee); AddHistory(employee, "Employee management"); await SyncGroups(employee, Input.GroupIds); await db.SaveChangesAsync(); Message = "Employee saved."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostBatchAsync()
    {
        var rows = BatchRows.Where(row => !string.IsNullOrWhiteSpace(row.EmployeeId) || !string.IsNullOrWhiteSpace(row.FirstName) || !string.IsNullOrWhiteSpace(row.LastName) || !string.IsNullOrWhiteSpace(row.JobGrade) || !string.IsNullOrWhiteSpace(row.Process) || !string.IsNullOrWhiteSpace(row.Accommodation) || !string.IsNullOrWhiteSpace(row.TelephoneNumber) || !string.IsNullOrWhiteSpace(row.Remark)).ToList();
        if (rows.Count == 0) return await ShowError("Paste at least one employee row from Excel.");
        if (string.IsNullOrWhiteSpace(BatchWorkshop) || string.IsNullOrWhiteSpace(BatchShift)) return await ShowError("Select a Workshop and Shift above the grid before saving.");
        var ids = rows.Select(x => x.EmployeeId.Trim()).ToList();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count) return await ShowError("Each pasted row needs a unique Emp ID.");
        if (rows.Any(row => string.IsNullOrWhiteSpace(row.FirstName))) return await ShowError("Each pasted row needs a First Name.");
        var workshop = await db.Workshops.FirstOrDefaultAsync(x => x.Name == BatchWorkshop.Trim()); if (workshop is null) return await ShowError($"Workshop '{BatchWorkshop}' was not found.");
        var badStatus = rows.Select(x => (x.Status ?? "").Trim()).FirstOrDefault(x => !Enum.TryParse<EmploymentState>(x, true, out _)); if (badStatus is not null) return await ShowError($"Status '{badStatus}' must be Active or Resigned.");
        if (BatchShift.Trim() is not ("A" or "B" or "C" or "Day")) return await ShowError("Select Shift A, B, C or Day above the grid.");
        var allGroups = await db.SpecialGroups.ToListAsync();
        var rowGroupNames = rows.Select(row => ParseGroupNames(row.Groups)).ToList();
        var unknownGroup = rowGroupNames.SelectMany(names => names).FirstOrDefault(name => !allGroups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        if (unknownGroup is not null) return await ShowError($"Special group '{unknownGroup}' was not found. Add it on the Special Groups page first.");
        var idSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        var existing = (await db.Employees.Where(x => idSet.Contains(x.EmployeeId)).ToListAsync()).ToDictionary(x => x.EmployeeId, StringComparer.OrdinalIgnoreCase); var added = 0;
        for (var i = 0; i < rows.Count; i++) { var row = rows[i]; var employeeId = row.EmployeeId.Trim(); if (!existing.TryGetValue(employeeId, out var employee)) { employee = new Employee { EmployeeId = employeeId, StartDate = DateOnly.FromDateTime(DateTime.Today) }; db.Employees.Add(employee); added++; } Apply(new EmployeeInput { EmployeeId = employeeId, FullName = $"{(row.FirstName ?? "").Trim()} {(row.LastName ?? "").Trim()}".Trim(), JobGrade = (row.JobGrade ?? "").Trim(), Process = (row.Process ?? "").Trim(), WorkshopId = workshop.Id, Shift = BatchShift.Trim(), Accommodation = (row.Accommodation ?? "").Trim(), TelephoneNumber = (row.TelephoneNumber ?? "").Trim(), EmploymentState = Enum.Parse<EmploymentState>((row.Status ?? "").Trim(), true), Remark = (row.Remark ?? "").Trim() }, employee); AddHistory(employee, "Employee batch grid"); var groupIds = allGroups.Where(g => rowGroupNames[i].Any(name => name.Equals(g.Name, StringComparison.OrdinalIgnoreCase))).Select(g => g.Id).ToList(); await SyncGroups(employee, groupIds); }
        await db.SaveChangesAsync(); Message = $"Saved {rows.Count} employee row(s) ({added} new)."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostGridSaveAsync()
    {
        var rowIds = GridRows.Select(r => r.Id).ToHashSet();
        var employees = (await db.Employees.ToListAsync()).Where(x => rowIds.Contains(x.Id)).ToDictionary(x => x.Id);
        var workshopIds = (await db.Workshops.ToListAsync()).Select(x => x.Id).ToHashSet();
        var employeeIdOwners = employees.Values.ToDictionary(x => x.EmployeeId, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var allGroups = await db.SpecialGroups.ToListAsync();
        var rowGroupNames = GridRows.Select(row => row.Groups ?? []).ToList();
        var unknownGroup = rowGroupNames.SelectMany(names => names).FirstOrDefault(name => !allGroups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        if (unknownGroup is not null) return await ShowError($"Special group '{unknownGroup}' was not found. Add it on the Special Groups page first.");
        for (var i = 0; i < GridRows.Count; i++)
        {
            var row = GridRows[i];
            if (!employees.TryGetValue(row.Id, out var employee)) continue;
            if (string.IsNullOrWhiteSpace(row.EmployeeId) || string.IsNullOrWhiteSpace(row.FullName)) return await ShowError("Emp ID and Name are required for every row.");
            if (!workshopIds.Contains(row.WorkshopId)) return await ShowError("Select a valid Workshop for every row.");
            if (row.Shift is not ("A" or "B" or "C" or "Day")) return await ShowError("Select a valid Shift for every row.");
            if (!Enum.TryParse<EmploymentState>(row.EmploymentState, true, out var state)) return await ShowError("Select a valid Status for every row.");
            if (employeeIdOwners.TryGetValue(row.EmployeeId, out var ownerId) && ownerId != row.Id) return await ShowError($"Emp ID '{row.EmployeeId}' already exists.");
            employeeIdOwners.Remove(employee.EmployeeId);
            employeeIdOwners[row.EmployeeId] = row.Id;
            employee.EmployeeId = row.EmployeeId;
            Apply(new EmployeeInput { EmployeeId = row.EmployeeId, FullName = row.FullName, JobGrade = row.JobGrade, Process = row.Process, WorkshopId = row.WorkshopId, Shift = row.Shift, Accommodation = row.Accommodation, TelephoneNumber = row.TelephoneNumber, EmploymentState = state, Remark = row.Remark }, employee);
            AddHistory(employee, "Employee grid edit");
            var groupIds = allGroups.Where(g => rowGroupNames[i].Any(name => name.Equals(g.Name, StringComparison.OrdinalIgnoreCase))).Select(g => g.Id).ToList();
            await SyncGroups(employee, groupIds);
        }
        await db.SaveChangesAsync(); Message = "Employee grid changes saved."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null) return RedirectToPage();
        db.Employees.Remove(employee);
        try { await db.SaveChangesAsync(); Message = "Employee deleted."; }
        catch (DbUpdateException) { return await ShowError("Cannot delete this employee because related records (e.g. resignation tickets) still reference them."); }
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostBulkDeleteAsync(List<int> selectedIds)
    {
        if (selectedIds is null || selectedIds.Count == 0) return await ShowError("Select at least one employee to delete.");
        var idSet = selectedIds.ToHashSet();
        var employees = await db.Employees.Where(x => idSet.Contains(x.Id)).ToListAsync();
        db.Employees.RemoveRange(employees);
        try { await db.SaveChangesAsync(); Message = $"Deleted {employees.Count} employee(s)."; }
        catch (DbUpdateException) { return await ShowError("Cannot delete one or more selected employees because related records (e.g. resignation tickets) still reference them."); }
        return RedirectToPage();
    }
    private async Task<IActionResult> ShowError(string error) { Error = error; await Load(); AddBlankBatchRows(); return Page(); }
    private void AddBlankBatchRows() { while (BatchRows.Count < 20) BatchRows.Add(new BatchEmployeeInput()); }
    private static EmployeeInput FromEmployee(Employee e) => new() { Id = e.Id, EmployeeId = e.EmployeeId, FullName = e.FullName, JobGrade = e.JobGrade, Process = e.Process, WorkshopId = e.CurrentWorkshopId, Shift = e.Shift, Accommodation = e.Accommodation, TelephoneNumber = e.TelephoneNumber, EmploymentState = e.EmploymentState, Remark = e.Remark, GroupIds = e.SpecialGroups.Select(x => x.SpecialGroupId).ToList() };
    private static void Apply(EmployeeInput input, Employee employee) { employee.FullName = input.FullName; employee.JobGrade = input.JobGrade ?? ""; employee.Process = input.Process ?? ""; employee.CurrentWorkshopId = input.WorkshopId; employee.Shift = input.Shift; employee.Accommodation = input.Accommodation ?? ""; employee.TelephoneNumber = input.TelephoneNumber ?? ""; employee.EmploymentState = input.EmploymentState; employee.Remark = input.Remark ?? ""; }
    private void AddHistory(Employee employee, string action) => db.EmployeeHistories.Add(new EmployeeHistory { Employee = employee, WorkshopId = employee.CurrentWorkshopId, EmploymentType = employee.EmploymentType, EffectiveDate = DateOnly.FromDateTime(DateTime.Today), ChangedBy = User.Identity?.Name ?? action });
    private static List<string> ParseGroupNames(string? groups) => (groups ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    private async Task SyncGroups(Employee employee, List<int> groupIds)
    {
        if (employee.Id != 0) await db.Entry(employee).Collection(x => x.SpecialGroups).LoadAsync();
        var wantedIds = groupIds.Distinct().ToHashSet();
        employee.SpecialGroups.Where(x => !wantedIds.Contains(x.SpecialGroupId)).ToList().ForEach(x => employee.SpecialGroups.Remove(x));
        var existingIds = employee.SpecialGroups.Select(x => x.SpecialGroupId).ToHashSet();
        foreach (var id in wantedIds.Where(id => !existingIds.Contains(id))) employee.SpecialGroups.Add(new EmployeeSpecialGroup { Employee = employee, SpecialGroupId = id });
    }
    private async Task Load() { Employees = await db.Employees.Include(x => x.CurrentWorkshop).Include(x => x.SpecialGroups).ThenInclude(x => x.SpecialGroup).OrderBy(x => x.EmployeeId).ToListAsync(); var workshops = await db.Workshops.ToListAsync(); WorkshopOptions = SeedData.EmployeeWorkshopNames.Select(name => workshops.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).Where(x => x is not null).Select(x => new SelectListItem(x!.Name, x.Id.ToString())).ToList(); if (Input.Id is not null && Employees.FirstOrDefault(x => x.Id == Input.Id)?.CurrentWorkshop is Workshop current && WorkshopOptions.All(x => x.Value != current.Id.ToString())) WorkshopOptions.Add(new SelectListItem(current.Name, current.Id.ToString())); GroupOptions = await db.SpecialGroups.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(); }
}
