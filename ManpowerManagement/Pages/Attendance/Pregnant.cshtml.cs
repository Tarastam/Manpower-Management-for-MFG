using ManpowerManagement.Data;
using ManpowerManagement.Models;
using ManpowerManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ManpowerManagement.Pages.Attendance;

public class PregnantModel(AppDbContext db) : PageModel
{
    public const string GroupName = "Pregnant";
    public const string ShiftKey = "Pregnant";
    public const decimal StandardHours = 8m;
    public const decimal MaxWeeklyHours = 48m;

    [BindProperty(SupportsGet = true)] public string StartMonth { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string EndMonth { get; set; } = "";
    [BindProperty] public List<IndexModel.GridRow> GridRows { get; set; } = [];
    [BindProperty] public DateOnly? SubmitDate { get; set; }
    public List<DateOnly> Days { get; set; } = [];
    public List<IndexModel.WeekGroup> WeekGroups { get; set; } = [];
    public string? Error { get; set; }
    public bool GroupMissing { get; set; }

    public async Task OnGetAsync()
    {
        SetCalendarRange();
        var employees = await db.Employees
            .Where(e => e.EmploymentState == EmploymentState.Active && e.SpecialGroups.Any(g => g.SpecialGroup!.Name == GroupName))
            .OrderBy(e => e.FullName).ToListAsync();
        if (!await db.SpecialGroups.AnyAsync(g => g.Name == GroupName)) { GroupMissing = true; }
        var employeeIds = employees.Select(e => e.Id).ToHashSet();
        var entries = await db.AttendanceEntries
            .Where(e => e.Shift == ShiftKey && e.BusinessDate >= Days.First() && e.BusinessDate <= Days.Last() && employeeIds.Contains(e.EmployeeId))
            .ToDictionaryAsync(e => (e.EmployeeId, e.BusinessDate));
        var offDays = Days.Where(IsDefaultOffDay).ToHashSet();
        GridRows = employees.Select(employee => new IndexModel.GridRow
        {
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeShift = employee.Shift,
            Cells = Days.Select(day =>
            {
                var isOffDay = offDays.Contains(day);
                if (isOffDay)
                {
                    return entries.TryGetValue((employee.Id, day), out var overtimeEntry)
                        ? new IndexModel.AttendanceCell { BusinessDate = day, WorkingHours = overtimeEntry.WorkingHours, WorkshopId = employee.CurrentWorkshopId, IsOffDay = true, IsConfirmed = overtimeEntry.IsConfirmed }
                        : new IndexModel.AttendanceCell { BusinessDate = day, WorkshopId = employee.CurrentWorkshopId, WorkingHours = 0, IsOffDay = true };
                }
                return entries.TryGetValue((employee.Id, day), out var entry)
                    ? new IndexModel.AttendanceCell { BusinessDate = day, WorkingHours = entry.WorkingHours, WorkshopId = employee.CurrentWorkshopId, LeaveType = entry.LeaveType, LeaveHours = entry.LeaveHours, IsConfirmed = entry.IsConfirmed }
                    : new IndexModel.AttendanceCell { BusinessDate = day, WorkshopId = employee.CurrentWorkshopId, WorkingHours = StandardHours };
            }).ToList()
        }).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        SetCalendarRange();
        var today = BusinessDateService.Current();
        if (SubmitDate is not DateOnly submitDate || submitDate < Days.First() || submitDate > Days.Last()) { Error = "Select a valid date to submit."; return Page(); }
        if (!User.IsInRole(Roles.Admin) && submitDate < today) { Error = "Only Admin can edit a previous business date."; return Page(); }
        var employeeIds = GridRows.Select(r => r.EmployeeId).ToHashSet();
        var existingEntries = await db.AttendanceEntries
            .Where(e => e.Shift == ShiftKey && e.BusinessDate >= Days.First() && e.BusinessDate <= Days.Last() && employeeIds.Contains(e.EmployeeId))
            .ToDictionaryAsync(e => (e.EmployeeId, e.BusinessDate));
        var offDays = Days.Where(IsDefaultOffDay).ToHashSet();
        foreach (var row in GridRows)
            foreach (var cell in row.Cells)
                cell.IsOffDay = offDays.Contains(cell.BusinessDate);

        foreach (var row in GridRows)
        {
            foreach (var week in WeekGroups)
            {
                var weeklyHours = row.Cells.Skip(week.StartIndex).Take(week.Count).Sum(cell => cell.WorkingHours);
                if (weeklyHours > MaxWeeklyHours)
                {
                    Error = $"Weekly working hours for {row.EmployeeName} ({week.Start:dd MMM} - {week.End:dd MMM yyyy}) cannot exceed {MaxWeeklyHours:0} hours.";
                    return Page();
                }
            }

            var cell = row.Cells.FirstOrDefault(c => c.BusinessDate == submitDate);
            if (cell == null) continue;
            if (cell.IsOffDay && (cell.LeaveType != LeaveType.None || cell.LeaveHours != 0)) { Error = $"OFF day entries for {row.EmployeeName} on {cell.BusinessDate:dd MMM yyyy} can contain working hours only."; return Page(); }
            if (cell.WorkingHours < 0 || cell.LeaveHours < 0 || cell.WorkingHours + cell.LeaveHours > StandardHours) { Error = $"Hours for {row.EmployeeName} on {cell.BusinessDate:dd MMM yyyy} must total at most {StandardHours}."; return Page(); }
            var employee = await db.Employees.FindAsync(row.EmployeeId);
            if (employee == null) continue;
            existingEntries.TryGetValue((row.EmployeeId, cell.BusinessDate), out var entry);
            if (entry is { IsConfirmed: true }) { Error = $"{row.EmployeeName} on {cell.BusinessDate:dd MMM yyyy} is already submitted. Edit that day individually in the table instead of using Submit attendance."; return Page(); }
            var expectedHours = cell.IsOffDay ? 0 : StandardHours;
            var changed = entry == null
                ? cell.WorkingHours != expectedHours || cell.LeaveType != LeaveType.None || cell.LeaveHours != 0
                : entry.WorkingHours != cell.WorkingHours || entry.LeaveType != cell.LeaveType || entry.LeaveHours != cell.LeaveHours;
            if (!changed) continue;
            if (entry == null) { entry = new AttendanceEntry { BusinessDate = cell.BusinessDate, Shift = ShiftKey, EmployeeId = row.EmployeeId }; db.AttendanceEntries.Add(entry); }
            entry.WorkshopId = employee.CurrentWorkshopId; entry.WorkingHours = cell.WorkingHours; entry.LeaveType = cell.LeaveType; entry.LeaveHours = cell.LeaveHours; entry.IsConfirmed = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        }
        db.AuditLogs.Add(new AuditLog { EntityName = "Attendance", EntityKey = $"{submitDate:yyyy-MM-dd}:pregnant", Action = "Confirmed day", Actor = User.Identity?.Name ?? "Supervisor" });
        await db.SaveChangesAsync(); return RedirectToPage(new { startMonth = StartMonth, endMonth = EndMonth });
    }

    public async Task<IActionResult> OnPostCellAsync(int employeeId, DateOnly businessDate, decimal workingHours, LeaveType leaveType, decimal leaveHours)
    {
        var today = BusinessDateService.Current();
        if (!User.IsInRole(Roles.Admin) && businessDate < today) return new JsonResult(new { ok = false, error = "Only Admin can edit a previous business date." });

        var employee = await db.Employees.FindAsync(employeeId);
        if (employee == null) return new JsonResult(new { ok = false, error = "Employee not found." });

        var isOffDay = IsDefaultOffDay(businessDate);
        if (isOffDay && (leaveType != LeaveType.None || leaveHours != 0)) return new JsonResult(new { ok = false, error = "OFF day entries can contain working hours only." });
        if (workingHours < 0 || leaveHours < 0 || workingHours + leaveHours > StandardHours) return new JsonResult(new { ok = false, error = $"Hours must total at most {StandardHours}." });

        var weekStart = businessDate.AddDays(-(int)businessDate.DayOfWeek);
        var weekEnd = weekStart.AddDays(6);
        var weeklyHours = await db.AttendanceEntries
            .Where(e => e.EmployeeId == employeeId && e.Shift == ShiftKey && e.BusinessDate >= weekStart && e.BusinessDate <= weekEnd && e.BusinessDate != businessDate)
            .SumAsync(e => (decimal?)e.WorkingHours) ?? 0;
        if (weeklyHours + workingHours > MaxWeeklyHours) return new JsonResult(new { ok = false, error = $"Weekly working hours cannot exceed {MaxWeeklyHours:0} hours." });

        var entry = await db.AttendanceEntries.FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Shift == ShiftKey && e.BusinessDate == businessDate);
        if (entry == null) { entry = new AttendanceEntry { BusinessDate = businessDate, Shift = ShiftKey, EmployeeId = employeeId }; db.AttendanceEntries.Add(entry); }
        entry.WorkshopId = employee.CurrentWorkshopId; entry.WorkingHours = workingHours; entry.LeaveType = leaveType; entry.LeaveHours = leaveHours; entry.IsConfirmed = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { EntityName = "Attendance", EntityKey = $"{businessDate:yyyy-MM-dd}:{employeeId}:pregnant", Action = "Edited single day", Actor = User.Identity?.Name ?? "Supervisor" });
        await db.SaveChangesAsync();
        return new JsonResult(new { ok = true, weeklyTotal = weeklyHours + workingHours });
    }

    void SetCalendarRange()
    {
        var current = BusinessDateService.Current(); var defaultStart = new DateOnly(current.Year, current.Month, 1).AddMonths(-1); var start = ParseMonth(StartMonth, defaultStart); var end = ParseMonth(EndMonth, start.AddMonths(2));
        if (end < start) end = start; if (MonthsApart(start, end) > 2) end = start.AddMonths(2);
        StartMonth = start.ToString("yyyy-MM"); EndMonth = end.ToString("yyyy-MM"); var lastDay = end.AddMonths(1).AddDays(-1);
        Days = Enumerable.Range(0, lastDay.DayNumber - start.DayNumber + 1).Select(i => start.AddDays(i)).ToList();
        WeekGroups = Days.Select((day, index) => new { day, index })
            .GroupBy(x => x.day.AddDays(-(int)x.day.DayOfWeek))
            .Select(group => new IndexModel.WeekGroup(group.First().day, group.Last().day, group.First().index, group.Count()))
            .ToList();
    }
    static DateOnly ParseMonth(string? value, DateOnly fallback) => DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", out var parsed) ? new DateOnly(parsed.Year, parsed.Month, 1) : fallback;
    static int MonthsApart(DateOnly start, DateOnly end) => (end.Year - start.Year) * 12 + end.Month - start.Month;
    static bool IsDefaultOffDay(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
