using System.Text;
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
    internal static readonly IReadOnlyDictionary<string, DateOnly> ShiftReferenceWorkingDays = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase)
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
    [BindProperty] public DateOnly? SubmitStartDate { get; set; }
    [BindProperty] public DateOnly? SubmitEndDate { get; set; }
    public List<SelectListItem> WorkshopOptions { get; set; } = [];
    public List<SelectListItem> ShiftOptions { get; set; } = [];
    public List<DateOnly> Days { get; set; } = [];
    public List<WeekGroup> WeekGroups { get; set; } = [];
    public HashSet<DateOnly> SubmittedDays { get; } = [];
    public HashSet<DateOnly> PartiallySubmittedDays { get; } = [];
    public DateOnly? LastSubmittedDay { get; set; }
    public string? Error { get; set; }
    public bool IsAdmin => User.IsInRole(Roles.Admin);

    public async Task OnGetAsync()
    {
        await Options(); SetCalendarRange();
        if (WorkshopId is not int workshopId) return;
        var isAllShifts = string.IsNullOrEmpty(Shift);
        // Pregnant employees are recorded on the Pregnant page only, so they must not also appear by shift.
        var employeesQuery = db.Employees.Where(e => e.EmploymentState == EmploymentState.Active && e.CurrentWorkshopId == workshopId && !e.SpecialGroups.Any(g => g.SpecialGroup!.Name == PregnantModel.GroupName));
        if (!isAllShifts) employeesQuery = employeesQuery.Where(e => e.Shift == Shift);
        var employees = await employeesQuery.OrderBy(e => e.Shift).ThenBy(e => e.FullName).ToListAsync();
        var employeeIds = employees.Select(e => e.Id).ToHashSet();
        var entriesQuery = db.AttendanceEntries.Where(e => e.BusinessDate >= Days.First() && e.BusinessDate <= Days.Last() && employeeIds.Contains(e.EmployeeId));
        if (!isAllShifts) entriesQuery = entriesQuery.Where(e => e.Shift == Shift);
        var entries = await entriesQuery.ToDictionaryAsync(e => (e.EmployeeId, e.BusinessDate));
        var workCalendar = (await db.WorkCalendars.Where(c => c.WorkshopId == workshopId && c.WorkDate >= Days.First() && c.WorkDate <= Days.Last()).Select(c => new { c.WorkDate, c.IsWorkingDay }).ToListAsync()).ToDictionary(c => c.WorkDate, c => c.IsWorkingDay);
        // An imported calendar always takes precedence. Until it is imported, each shift follows its 4-work / 2-OFF cycle.
        var offDaysByShift = employees.Select(e => e.Shift).Distinct().ToDictionary(shift => shift, shift => Days.Where(day => workCalendar.TryGetValue(day, out var isWorkingDay)
            ? !isWorkingDay
            : IsDefaultOffDay(shift, day)).ToHashSet());
        GridRows = employees.Select(employee => new GridRow
        {
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeShift = employee.Shift,
            Cells = Days.Select(day =>
            {
                var isOffDay = offDaysByShift[employee.Shift].Contains(day);
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
        // A day is submitted when every listed employee has a confirmed entry; partial when only some do (e.g. leave recorded in advance).
        foreach (var (day, index) in Days.Select((day, index) => (day, index)))
        {
            var confirmed = GridRows.Count(row => row.Cells[index].IsConfirmed);
            if (confirmed == 0) continue;
            if (confirmed == GridRows.Count) SubmittedDays.Add(day); else PartiallySubmittedDays.Add(day);
        }
        LastSubmittedDay = SubmittedDays.Count == 0 ? null : SubmittedDays.Max();
    }

    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        if (WorkshopId is not int workshopId) { Error = "Select a workshop."; await Options(); SetCalendarRange(); return Page(); }
        await OnGetAsync();
        var recordedDays = Days.Where(day => GridRows.Any(row => row.Cells.Any(cell => cell.BusinessDate == day && cell.IsConfirmed))).ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[] { "Emp ID", "Employee", "Shift" }.Concat(recordedDays.Select(d => d.ToString("yyyy-MM-dd"))).Select(CsvField)));
        foreach (var row in GridRows)
        {
            var cells = recordedDays.Select(day =>
            {
                var cell = row.Cells.First(c => c.BusinessDate == day);
                if (!cell.IsConfirmed) return "";
                if (cell.IsOffDay && cell.WorkingHours == 0) return "OFF";
                var parts = new List<string>();
                if (cell.WorkingHours != 0) parts.Add($"{cell.WorkingHours:0.##}h");
                if (cell.LeaveType != LeaveType.None) parts.Add($"{cell.LeaveType}:{cell.LeaveHours:0.##}h");
                if (cell.IsOffDay) parts.Add("OFF");
                return parts.Count == 0 ? "" : string.Join(" ", parts);
            });
            sb.AppendLine(string.Join(',', new[] { row.EmployeeCode, row.EmployeeName, row.EmployeeShift }.Concat(cells).Select(CsvField)));
        }
        var fileName = $"attendance_{workshopId}_{(string.IsNullOrEmpty(Shift) ? "All" : Shift)}_{StartMonth}_to_{EndMonth}.csv";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", fileName);
    }
    private static string CsvField(string? value)
    {
        value ??= "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await Options(); SetCalendarRange();
        if (WorkshopId is not int workshopId) { Error = "Select a workshop."; return Page(); }
        var today = BusinessDateService.Current();
        var isAdmin = User.IsInRole(Roles.Admin);
        if (SubmitStartDate is not DateOnly submitStart || submitStart < Days.First() || submitStart > Days.Last()) { Error = "Select a valid date to submit."; return Page(); }
        var submitEnd = SubmitEndDate is DateOnly requestedEnd && requestedEnd >= submitStart && requestedEnd <= Days.Last() ? requestedEnd : submitStart;
        if (!isAdmin) submitEnd = submitStart;
        var isAllShifts = string.IsNullOrEmpty(Shift);
        var employeeIds = GridRows.Select(r => r.EmployeeId).ToHashSet();
        var existingEntriesQuery = db.AttendanceEntries.Where(e => e.BusinessDate >= Days.First() && e.BusinessDate <= Days.Last() && employeeIds.Contains(e.EmployeeId));
        if (!isAllShifts) existingEntriesQuery = existingEntriesQuery.Where(e => e.Shift == Shift);
        var existingEntries = await existingEntriesQuery.ToDictionaryAsync(e => (e.EmployeeId, e.BusinessDate));
        var workCalendar = (await db.WorkCalendars.Where(c => c.WorkshopId == workshopId && c.WorkDate >= Days.First() && c.WorkDate <= Days.Last()).Select(c => new { c.WorkDate, c.IsWorkingDay }).ToListAsync()).ToDictionary(c => c.WorkDate, c => c.IsWorkingDay);
        var offDaysByShift = GridRows.Select(r => r.EmployeeShift).Distinct().ToDictionary(shift => shift, shift => Days.Where(day => workCalendar.TryGetValue(day, out var isWorkingDay)
            ? !isWorkingDay
            : IsDefaultOffDay(shift, day)).ToHashSet());
        foreach (var row in GridRows)
            foreach (var cell in row.Cells)
                cell.IsOffDay = offDaysByShift[row.EmployeeShift].Contains(cell.BusinessDate);

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
        }

        var changes = new List<(GridRow Row, AttendanceCell Cell, AttendanceEntry? Entry, int ActualWorkshop)>();
        foreach (var row in GridRows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.BusinessDate < submitStart || cell.BusinessDate > submitEnd) continue;
                existingEntries.TryGetValue((row.EmployeeId, cell.BusinessDate), out var entry);
                if (entry is { IsConfirmed: true }) continue;
                var actualWorkshop = cell.WorkshopId == 0 ? workshopId : cell.WorkshopId;

                if (cell.BusinessDate < today && !isAdmin) { Error = "Only Admin can edit a previous business date."; return Page(); }
                if (cell.BusinessDate > today && cell.LeaveType == LeaveType.None) { Error = $"Working hours for {row.EmployeeName} on {cell.BusinessDate:dd MMM yyyy} cannot be entered in advance. Only leave can be recorded ahead of today."; return Page(); }
                if (cell.IsOffDay && (cell.LeaveType != LeaveType.None || cell.LeaveHours != 0)) { Error = $"OFF day entries for {row.EmployeeName} on {cell.BusinessDate:dd MMM yyyy} can contain working hours only."; return Page(); }
                if (cell.WorkingHours < 0 || cell.LeaveHours < 0 || cell.WorkingHours + cell.LeaveHours > StandardHours) { Error = $"Hours for {row.EmployeeName} on {cell.BusinessDate:dd MMM yyyy} must total at most {StandardHours}."; return Page(); }

                changes.Add((row, cell, entry, actualWorkshop));
            }
        }

        if (changes.Count == 0) { Error = "All days in the selected date range are already submitted."; return Page(); }

        foreach (var (row, cell, existingEntry, actualWorkshop) in changes)
        {
            var entry = existingEntry;
            if (entry == null) { entry = new AttendanceEntry { BusinessDate = cell.BusinessDate, Shift = row.EmployeeShift, EmployeeId = row.EmployeeId }; db.AttendanceEntries.Add(entry); }
            entry.WorkshopId = actualWorkshop; entry.WorkingHours = cell.WorkingHours; entry.LeaveType = cell.LeaveType; entry.LeaveHours = cell.LeaveHours; entry.IsConfirmed = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        }
        var rangeKey = submitStart == submitEnd ? $"{submitStart:yyyy-MM-dd}" : $"{submitStart:yyyy-MM-dd}_to_{submitEnd:yyyy-MM-dd}";
        db.AuditLogs.Add(new AuditLog { EntityName = "Attendance", EntityKey = $"{rangeKey}:{workshopId}:{Shift}", Action = "Confirmed days", Actor = User.Identity?.Name ?? "Supervisor" });
        await db.SaveChangesAsync(); return RedirectToPage(new { workshopId, shift = Shift, startMonth = StartMonth, endMonth = EndMonth });
    }

    public async Task<IActionResult> OnPostCellAsync(int employeeId, DateOnly businessDate, decimal workingHours, int cellWorkshopId, LeaveType leaveType, decimal leaveHours)
    {
        if (WorkshopId is not int workshopId) return new JsonResult(new { ok = false, error = "Select a workshop." });
        var today = BusinessDateService.Current();
        if (!User.IsInRole(Roles.Admin) && businessDate < today) return new JsonResult(new { ok = false, error = "Only Admin can edit a previous business date." });
        if (businessDate > today && leaveType == LeaveType.None) return new JsonResult(new { ok = false, error = "Working hours cannot be entered in advance. Only leave can be recorded ahead of today." });

        var employeeShift = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.Shift).FirstOrDefaultAsync();
        if (employeeShift == null) return new JsonResult(new { ok = false, error = "Employee not found." });

        var isOffDay = (await db.WorkCalendars.Where(c => c.WorkshopId == workshopId && c.WorkDate == businessDate).Select(c => (bool?)c.IsWorkingDay).FirstOrDefaultAsync()) is bool isWorkingDay
            ? !isWorkingDay
            : IsDefaultOffDay(employeeShift, businessDate);
        if (isOffDay && (leaveType != LeaveType.None || leaveHours != 0)) return new JsonResult(new { ok = false, error = "OFF day entries can contain working hours only." });
        if (workingHours < 0 || leaveHours < 0 || workingHours + leaveHours > StandardHours) return new JsonResult(new { ok = false, error = $"Hours must total at most {StandardHours}." });

        var actualWorkshop = cellWorkshopId == 0 ? workshopId : cellWorkshopId;
        var weekStart = businessDate.AddDays(-(int)businessDate.DayOfWeek);
        var weekEnd = weekStart.AddDays(6);
        var weeklyHours = await db.AttendanceEntries
            .Where(e => e.EmployeeId == employeeId && e.Shift == employeeShift && e.BusinessDate >= weekStart && e.BusinessDate <= weekEnd && e.BusinessDate != businessDate)
            .SumAsync(e => (decimal?)e.WorkingHours) ?? 0;
        if (weeklyHours + workingHours > MaxWeeklyHours) return new JsonResult(new { ok = false, error = $"Weekly working hours cannot exceed {MaxWeeklyHours:0} hours." });

        var entry = await db.AttendanceEntries.FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Shift == employeeShift && e.BusinessDate == businessDate);
        if (entry == null) { entry = new AttendanceEntry { BusinessDate = businessDate, Shift = employeeShift, EmployeeId = employeeId }; db.AttendanceEntries.Add(entry); }
        entry.WorkshopId = actualWorkshop; entry.WorkingHours = workingHours; entry.LeaveType = leaveType; entry.LeaveHours = leaveHours; entry.IsConfirmed = true; entry.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { EntityName = "Attendance", EntityKey = $"{businessDate:yyyy-MM-dd}:{employeeId}:{employeeShift}", Action = "Edited single day", Actor = User.Identity?.Name ?? "Supervisor" });
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
    internal static bool IsDefaultOffDay(string shift, DateOnly day)
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
        ShiftOptions = (await shifts.Select(e => e.Shift).Distinct().OrderBy(s => s).Select(s => new SelectListItem(s, s)).ToListAsync())
            .Prepend(new SelectListItem("All shift", "")).ToList();
    }
}
