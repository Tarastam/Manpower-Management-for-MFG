using ManpowerManagement.Data;
using ManpowerManagement.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 20000;
});
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ManpowerDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options => options.LoginPath = "/Account/Login");
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy("TicketsManage", policy => policy.RequireRole(Roles.Admin, Roles.Approver));
    options.AddPolicy("EmployeesManage", policy => policy.RequireRole(Roles.Admin, Roles.Approver));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/SpecialGroups", "AdminOnly");
    options.Conventions.AuthorizePage("/Tickets/Manage", "TicketsManage");
    options.Conventions.AuthorizePage("/Employees/Index", "EmployeesManage");
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    options.MaxModelBindingCollectionSize = 20000;
});

var app = builder.Build();
await SeedData.InitialiseAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
