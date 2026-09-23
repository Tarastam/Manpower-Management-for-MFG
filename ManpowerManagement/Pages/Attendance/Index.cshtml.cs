using ManpowerManagement.Data;
using ManpowerManagement.Models;
using ManpowerManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ManpowerManagement.Pages.Attendance;

public class IndexModel(AppDbContext db, IMemoryCache cache) : PageModel
{
    const string WorkshopOptionsCacheKey = "attendance:workshop-options";
    public const decimal StandardHours = 10.67m;
    public const decimal MaxWeeklyHours = 60m;
    // The September 2026 shift calendar uses one 4-work / 2-OFF cycle with staggered starts.
    // Imported calendar data takes precedence over these reference dates.
    static readonly IReadOnlyDictionary<string, DateOnly> ShiftReferenceWorkingDays = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = new(2026, 9, 3),
        ["B"] = new(2026, 9, 5),
        ["C"] = new(2026, 9, 1)
    };
    public class AttendanceCell { public DateOnly BusinessDate { get; set; } public decimal WorkingHours { get; set; } = StandardHours; public int WorkshopId { get; set; } public LeaveType LeaveType { get; set; } = LeaveType.None; public decimal LeaveHours { get; set; } public bool IsOffDay { get; set; } public bool IsConfirmed { get; set; } }
    public class GridRow { public int EmployeeId { get; set; } public string EmployeeCode { get; set; } = ""; public string EmployeeName { get; set; } = ""; public string EmployeeShift { get; set; } = ""; public List<AttendanceCell> Cells { get; set; } = []; }
    public record WeekGroup(DateOnly Start, DateOnly End, int StartIndex, int Count);

    [BindProperty(SupportsGet = true)] public int? WorkshopId { get; set; }
    [BindProperty(SupportsGet = true)] public string Shift { get; set; } = "A";
    [BindProperty(SupportsGet = true)] public string StartMonth { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string EndMonth { get; set; } = "";
    [BindProperty] public List<GridRow> GridRows { get; set; } = [];
    [BindProperty] public DateOnly? SubmitDate { get; set; }
    public List<SelectListItem> WorkshopOptions { get; set; } = [];
    public List<SelectListItem> ShiftOptions { get; set; } = [];
    public List<DateOnly> Days { get; set; } = [];
    public List<WeekGroup> WeekGroups { get; set; } = [];
    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        await Options(); SetCalendarRange();
        if (WorkshopId is not int workshopId) return;
        var employees = await db.Employees.Where(e => e.EmploymentState == EmploymentState.Active && e.CurrentWorkshopId == workshopId && e.Shift == Shift).OrderBy(e => e.FullName).ToListAsync();
        var employeeIds = employees.Select(e => e.Id).ToHashSet();
        var entries = await db.AttendanceEntries
            .Where(e => e.Shift == Shift && e.BusinessDate >= Days.First() && e.BusinessDate <= Days.Last() && employeeIds.Contains(e.EmployeeId))
            .ToDictionaryAsync(e => (e.EmployeeId, e.BusinessDate));
        var workCalendar = (await db.WorkCalendars.Where(c => c.WorkshopId == workshopId && c.WorkDate >= Days.First() && c.WorkDate <= Days.Last()).Select(c => new { c.WorkDate, c.IsWorkingDay }).ToListAsync()).ToDictionary(c => c.WorkDate, c => c.IsWorkingDay);
        // An imported calendar always takes precedence. Until it is imported, each shift follows its 4-work / 2-OFF cycle.
        var offDays = Days.Where(day => workCalendar.TryGetValue(day, out var isWorkingDay)
            ? !isWorkingDay
            : IsDefaultOffDay(Shift, day)).ToHashSet();
        GridRows = employees.Select(employee => new GridRow
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
                        ? new AttendanceCell { BusinessDate = day, WorkingHours = overtimeEntry.WorkingHours, WorkshopId = overtimeEntry.WorkshopId, IsOffDay = true, IsConfirmed = overtimeEntry.IsConfirmed }
                        : new AttendanceCell { BusinessDate = day, WorkshopId = workshopId, WorkingHours = 0, IsOffDay = true };
                }
                return entries.TryGetValue((employee.Id, day), out var entry)
                    ? new AttendanceCell { BusinessDate = day, WorkingHours = entry.WorkingHours, WorkshopId = entry.WorkshopId, LeaveType = entry.LeaveType, LeaveHours = entry.LeaveHours, IsConfirmed = entry.IsConfirmed }
                    : new AttendanceCell { BusinessDate = day, WorkshopId = workshopId };
            }).ToList()
        }).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await Options(); SetCalendarRange();
        if (WorkshopId is not int workshopId) { Error = "Select a workshop."; return Page(); }
        var today = BusinessDateService.Current();
        if (SubmitDate is not DateOnly submitDate || submitDate < Days.First() || submitDate > Days.Last()) { Error = "Select a valid date to submit."; return Page(); }
        if (!User.IsInRole(Roles.Admin) && submitDate < today) { Error = "Only Admin can edit a previous business date."; return Page(); }
        var employeeIds = GridRows.Select(r => r.EmployeeId).ToHashSet();
        var existingEntries = await db.AttendanceEntries
            .Where(e => e.Shift == Shift && e.BusinessDate >= Days.First() && e.BusinessDate <= Days.Last() && employeeIds.Contains(e.EmployeeId))
            .ToDictionaryAsync(e => (e.EmployeeId, e.BusinessDate));
        var workCalendar = (await db.WorkCalendars.Where(c => c.WorkshopId == workshopId && c.WorkDate >= Days.First() && c.WorkDate <= Days.Last()).Select(c => new { c.WorkDate, c.IsWorkingDay }).ToListAsync()).ToDictionary(c => c.WorkDate, c => c.IsWorkingDay);
        var offDays = Days.Where(day => workCalendar.TryGetValue(day, out var isWorkingDay)
            ? !isWorkingDay
            : IsDefaultOffDay(Shift, day)).ToHashSet();
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
            var actualWorkshop = cell.WorkshopId == 0 ? workshopId : cell.WorkshopId;
            existingEntries.TryGetValue((row.EmployeeId, cell.BusinessDate), out var entry);
            if (entry is { IsConfirmed: true }) { Error = $"{row.EmployeeName} on {cell.BusinessDate:dd MMM yyyy} is already submitted. Edit that day individually in the table instead of using Submit attendance."; return Page(); }
            var expectedHours = cell.IsOffDay ? 0 : StandardHours;
            var changed = entry == null
                ? cell.WorkingHours != expectedHours || cell.LeaveType != LeaveType.None || cell.LeaveHours != 0 || actualWorkshop != workshopId
                : entry.WorkshopId != actualWorkshop || entry.WorkingHours != cell.WorkingHours || entry.LeaveType != cell.LeaveType || entry.LeaveHours != cell.LeaveHours;
            if (!changed) continue;
            if (entry == null) { entry = new AttendanceEntry { BusinessDate = cell.BusinessDate, Shift = Shift, EmployeeId = row.EmployeeId }; db.AttendanceEntries.Add(entry); }
            entry.WorkshopId = actualWorkshop; entry.WorkingHours = cell.WorkingHours; entry.LeaveType = cell.LeaveType; entry.LeaveHours = cell.LeaveHours; entry.IsConfirmed = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        }
        db.AuditLogs.Add(new AuditLog { EntityName = "Attendance", EntityKey = $"{submitDate:yyyy-MM-dd}:{workshopId}:{Shift}", Action = "Confirmed day", Actor = User.Identity?.Name ?? "Supervisor" });
        await db.SaveChangesAsync(); return RedirectToPage(new { workshopId, shift = Shift, startMonth = StartMonth, endMonth = EndMonth });
    }

    public async Task<IActionResult> OnPostCellAsync(int employeeId, DateOnly businessDate, decimal workingHours, int cellWorkshopId, LeaveType leaveType, decimal leaveHours)
    {
        if (WorkshopId is not int workshopId) return new JsonResult(new { ok = false, error = "Select a workshop." });
        var today = BusinessDateService.Current();
        if (!User.IsInRole(Roles.Admin) && businessDate < today) return new JsonResult(new { ok = false, error = "Only Admin can edit a previous business date." });

        var isOffDay = (await db.WorkCalendars.Where(c => c.WorkshopId == workshopId && c.WorkDate == businessDate).Select(c => (bool?)c.IsWorkingDay).FirstOrDefaultAsync()) is bool isWorkingDay
            ? !isWorkingDay
            : IsDefaultOffDay(Shift, businessDate);
        if (isOffDay && (leaveType != LeaveType.None || leaveHours != 0)) return new JsonResult(new { ok = false, error = "OFF day entries can contain working hours only." });
        if (workingHours < 0 || leaveHours < 0 || workingHours + leaveHours > StandardHours) return new JsonResult(new { ok = false, error = $"Hours must total at most {StandardHours}." });

        var actualWorkshop = cellWorkshopId == 0 ? workshopId : cellWorkshopId;
        var weekStart = businessDate.AddDays(-(int)businessDate.DayOfWeek);
        var weekEnd = weekStart.AddDays(6);
        var weeklyHours = await db.AttendanceEntries
            .Where(e => e.EmployeeId == employeeId && e.Shift == Shift && e.BusinessDate >= weekStart && e.BusinessDate <= weekEnd && e.BusinessDate != businessDate)
            .SumAsync(e => (decimal?)e.WorkingHours) ?? 0;
        if (weeklyHours + workingHours > MaxWeeklyHours) return new JsonResult(new { ok = false, error = $"Weekly working hours cannot exceed {MaxWeeklyHours:0} hours." });

        var entry = await db.AttendanceEntries.FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Shift == Shift && e.BusinessDate == businessDate);
        if (entry == null) { entry = new AttendanceEntry { BusinessDate = businessDate, Shift = Shift, EmployeeId = employeeId }; db.AttendanceEntries.Add(entry); }
        entry.WorkshopId = actualWorkshop; entry.WorkingHours = workingHours; entry.LeaveType = leaveType; entry.LeaveHours = leaveHours; entry.IsConfirmed = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { EntityName = "Attendance", EntityKey = $"{businessDate:yyyy-MM-dd}:{employeeId}:{Shift}", Action = "Edited single day", Actor = User.Identity?.Name ?? "Supervisor" });
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
            .Select(group => new WeekGroup(group.First().day, group.Last().day, group.First().index, group.Count()))
            .ToList();
    }
    static DateOnly ParseMonth(string? value, DateOnly fallback) => DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", out var parsed) ? new DateOnly(parsed.Year, parsed.Month, 1) : fallback;
    static int MonthsApart(DateOnly start, DateOnly end) => (end.Year - start.Year) * 12 + end.Month - start.Month;
    static bool IsDefaultOffDay(string shift, DateOnly day)
    {
        if (!ShiftReferenceWorkingDays.TryGetValue(shift, out var referenceWorkingDay)) return false;
        var cycleDay = ((day.DayNumber - referenceWorkingDay.DayNumber) % 6 + 6) % 6;
        return cycleDay >= 4;
    }
    async Task Options()
    {
        var active = db.Employees.Where(e => e.EmploymentState == EmploymentState.Active);
        WorkshopOptions = await cache.GetOrCreateAsync(WorkshopOptionsCacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            return await active.Select(e => e.CurrentWorkshop!).Where(w => w != null).Distinct().OrderBy(w => w.Name).Select(w => new SelectListItem(w.Name, w.Id.ToString())).ToListAsync();
        }) ?? [];
        var shifts = active.AsQueryable(); if (WorkshopId is int workshopId) shifts = shifts.Where(e => e.CurrentWorkshopId == workshopId);
        ShiftOptions = await shifts.Select(e => e.Shift).Distinct().OrderBy(s => s).Select(s => new SelectListItem(s, s)).ToListAsync();
    }
}
