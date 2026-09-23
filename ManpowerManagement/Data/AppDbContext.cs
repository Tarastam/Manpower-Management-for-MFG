using ManpowerManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;

namespace ManpowerManagement.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeHistory> EmployeeHistories => Set<EmployeeHistory>();
    public DbSet<WorkCalendar> WorkCalendars => Set<WorkCalendar>();
    public DbSet<AttendanceEntry> AttendanceEntries => Set<AttendanceEntry>();
    public DbSet<ResignationTicket> ResignationTickets => Set<ResignationTicket>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SpecialGroup> SpecialGroups => Set<SpecialGroup>();
    public DbSet<EmployeeSpecialGroup> EmployeeSpecialGroups => Set<EmployeeSpecialGroup>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("mp");
        builder.Entity<Employee>().HasIndex(x => x.EmployeeId).IsUnique();
        builder.Entity<Employee>().Property(x => x.Shift).HasMaxLength(3);
        // Employee master values are entered as text and must remain text in mp.Employees.
        // Keep the CLR types for validation, date filtering, and enum logic in the application.
        var dateOnlyAsText = new ValueConverter<DateOnly, string>(
            value => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            value => DateOnly.Parse(value, CultureInfo.InvariantCulture));
        var nullableDateOnlyAsText = new ValueConverter<DateOnly?, string?>(
            value => value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            value => string.IsNullOrWhiteSpace(value) ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture));
        var employmentTypeAsText = new ValueConverter<EmploymentType, string>(
            value => value.ToString(),
            value => Enum.Parse<EmploymentType>(value, true));
        var employmentStateAsText = new ValueConverter<EmploymentState, string>(
            value => value.ToString(),
            value => Enum.Parse<EmploymentState>(value, true));
        builder.Entity<Employee>().Property(x => x.StartDate).HasConversion(dateOnlyAsText).HasColumnType("nvarchar(10)");
        builder.Entity<Employee>().Property(x => x.ResignedDate).HasConversion(nullableDateOnlyAsText).HasColumnType("nvarchar(10)");
        builder.Entity<Employee>().Property(x => x.EmploymentType).HasConversion(employmentTypeAsText).HasColumnType("nvarchar(32)");
        builder.Entity<Employee>().Property(x => x.EmploymentState).HasConversion(employmentStateAsText).HasColumnType("nvarchar(32)");
        builder.Entity<AttendanceEntry>().HasIndex(x => new { x.BusinessDate, x.Shift, x.EmployeeId }).IsUnique();
        builder.Entity<AttendanceEntry>().HasIndex(x => new { x.EmployeeId, x.BusinessDate, x.Shift });
        builder.Entity<WorkCalendar>().HasIndex(x => new { x.WorkshopId, x.WorkDate }).IsUnique();
        builder.Entity<Workshop>().HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AttendanceEntry>().Property(x => x.WorkingHours).HasPrecision(5, 2);
        builder.Entity<AttendanceEntry>().Property(x => x.LeaveHours).HasPrecision(5, 2);
        builder.Entity<AttendanceEntry>().HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<EmployeeHistory>().HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WorkCalendar>().HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ResignationTicket>().HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ResignationTicket>().HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SpecialGroup>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<EmployeeSpecialGroup>().HasKey(x => new { x.EmployeeId, x.SpecialGroupId });
        builder.Entity<EmployeeSpecialGroup>().HasOne(x => x.Employee).WithMany(x => x.SpecialGroups).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EmployeeSpecialGroup>().HasOne(x => x.SpecialGroup).WithMany().HasForeignKey(x => x.SpecialGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
