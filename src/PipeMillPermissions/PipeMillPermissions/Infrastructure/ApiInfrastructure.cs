using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PipeMillPermissions.Client.Shared;
using PipeMillPermissions.Data;
using PipeMillPermissions.Permissions;

namespace PipeMillPermissions.Infrastructure;

public static class ApiInfrastructure
{
    public static IResult Problem(HttpContext context, int status, string code, string message) =>
        Results.Json(new ProblemDetails { Status = status, Title = message, Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier } }, statusCode: status, contentType: "application/problem+json");

    public static async Task<IResult> DeleteSafely<T>(int id, AppDbContext db, HttpContext http) where T : class
    {
        var row = await db.Set<T>().FindAsync(id); if (row is null) return Problem(http, 404, "not_found", "رکورد مورد نظر پیدا نشد.");
        db.Remove(row);
        try { await db.SaveChangesAsync(); return Results.NoContent(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Problem(http, 409, "related_record_conflict", "به‌دلیل وجود رکوردهای وابسته، حذف امکان‌پذیر نیست."); }
    }
}

public static class AppSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, IWebHostEnvironment env)
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(); await scope.ServiceProvider.GetRequiredService<PermissionCatalogSynchronizer>().SyncAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Administrator", "Viewer", "Inspector" }) if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        if (!await db.Equipment.AnyAsync())
        {
            var e1 = new Equipment { Code = "EQ-100", Name = "دستگاه شکل‌دهی", ProductionLine = "خط یک", IsActive = true };
            var e2 = new Equipment { Code = "EQ-210", Name = "کوره عملیات حرارتی", ProductionLine = "خط دو", IsActive = true };
            db.AddRange(e1, e2); await db.SaveChangesAsync();
            var order = new ProductionOrder { OrderNumber = "PO-1403-001", EquipmentId = e1.Id, PlannedQuantity = 1200, StartDate = new DateOnly(2026, 11, 1), Status = "در حال تولید" };
            db.Add(order); await db.SaveChangesAsync();
            db.Add(new Inspection { InspectionNumber = "QC-001", ProductionOrderId = order.Id, InspectionDate = new DateOnly(2026, 11, 4), Result = "قابل قبول", Notes = "ابعاد در محدوده مجاز است." }); await db.SaveChangesAsync();
        }
        if (!env.IsDevelopment()) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var demoPassword = config["DemoAccounts:Password"] ?? Environment.GetEnvironmentVariable("PIPEMILL_DEMO_PASSWORD");
        if (string.IsNullOrWhiteSpace(demoPassword)) return;
        await EnsureUser("admin", "مدیر سامانه", "Administrator"); await EnsureUser("viewer", "کاربر مشاهده‌گر", "Viewer"); await EnsureUser("inspector", "بازرس کیفیت", "Inspector");
        await Grant("Administrator", db.PermissionCatalog.Where(x => x.Active).Select(x => x.Key).ToArrayAsync());
        await Grant("Viewer", ["equipment.list", "equipment.read", "production-orders.list", "production-orders.read", "inspections.list", "inspections.read"]);
        await Grant("Inspector", ["inspections.list", "inspections.read", "inspections.update", "inspections.approval-history.read"]);

        async Task EnsureUser(string name, string display, string role)
        {
            var user = await users.FindByNameAsync(name);
            if (user is null) { user = new AppUser { UserName = name, DisplayName = display }; var result = await users.CreateAsync(user, demoPassword); if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description))); }
            if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);
        }
        async Task Grant(string roleName, string[] keys)
        {
            var role = await roles.FindByNameAsync(roleName) ?? throw new InvalidOperationException();
            var ids = await db.PermissionCatalog.Where(x => keys.Contains(x.Key)).Select(x => x.Id).ToListAsync();
            var existing = await db.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync();
            db.RolePermissions.AddRange(ids.Except(existing).Select(id => new RolePermission { RoleId = role.Id, PermissionId = id })); await db.SaveChangesAsync();
        }
    }
}

public sealed class ApiStatusHandler : IStatusCodeHandler
{
    // PERMISSION-ERROR
    public async ValueTask HandleAsync(StatusCodeContext context)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api") && context.HttpContext.Response.StatusCode is 401 or 403)
        {
            var status = context.HttpContext.Response.StatusCode; context.HttpContext.Response.ContentType = "application/problem+json";
            await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = status == 401 ? "برای ادامه وارد سامانه شوید." : "شما اجازه انجام این عملیات را ندارید.", Extensions = { ["code"] = status == 401 ? "authentication_required" : "permission_denied", ["traceId"] = context.HttpContext.TraceIdentifier } });
        }
    }
}
