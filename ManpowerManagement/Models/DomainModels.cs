using Microsoft.AspNetCore.Identity;

namespace ManpowerManagement.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Approver = "ResignationApprover";
}

public enum EmploymentType { NewComer, Permanent }
public enum EmploymentState { Active, Resigned }
public enum LeaveType { None, AnnualLeave, SickLeave, BusinessLeave, OtherLeave, Absent, MotherLeave }
public enum TicketStatus { Submitted, Approved, Rejected }

public class ApplicationUser : IdentityUser { }

public class Workshop
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public Workshop? Parent { get; set; }
    public ICollection<Workshop> Children { get; set; } = new List<Workshop>();
}

public class Employee
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string JobGrade { get; set; } = string.Empty;
    public string Process { get; set; } = string.Empty;
    public string Accommodation { get; set; } = string.Empty;
    public string TelephoneNumber { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmploymentState EmploymentState { get; set; } = EmploymentState.Active;
    public DateOnly? ResignedDate { get; set; }
    public int CurrentWorkshopId { get; set; }
    public Workshop? CurrentWorkshop { get; set; }
    public string Shift { get; set; } = "Day";
    public ICollection<EmployeeHistory> History { get; set; } = new List<EmployeeHistory>();
    public ICollection<EmployeeSpecialGroup> SpecialGroups { get; set; } = new List<EmployeeSpecialGroup>();
}

public class SpecialGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#6c757d";
    public bool IsActive { get; set; } = true;
}

public class EmployeeSpecialGroup
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int SpecialGroupId { get; set; }
    public SpecialGroup? SpecialGroup { get; set; }
}

public class EmployeeHistory
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string ChangedBy { get; set; } = "Supervisor";
}

public class WorkCalendar
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    public DateOnly WorkDate { get; set; }
    public bool IsWorkingDay { get; set; }
}

public class AttendanceEntry
{
    public int Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string Shift { get; set; } = "A";
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    public decimal WorkingHours { get; set; }
    public LeaveType LeaveType { get; set; }
    public decimal LeaveHours { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ResignationTicket
{
    public int Id { get; set; }
    public string RequesterEmployeeId { get; set; } = string.Empty;
    public string RequesterShift { get; set; } = "Day";
    public int WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Submitted;
    public DateOnly? EffectiveResignedDate { get; set; }
    public string? DecisionBy { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public int Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? Detail { get; set; }
}
