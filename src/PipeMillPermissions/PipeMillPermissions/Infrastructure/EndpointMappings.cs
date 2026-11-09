using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PipeMillPermissions.Client.Shared;
using PipeMillPermissions.Data;
using PipeMillPermissions.Permissions;

namespace PipeMillPermissions.Infrastructure;

public static class EndpointMappings
{
    public static void MapApplicationApis(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", async (LoginRequest request, SignInManager<AppUser> signIn, HttpContext h) =>
        {
            var result = await signIn.PasswordSignInAsync(request.UserName, request.Password, false, true);
            return result.Succeeded ? Results.Ok() : ApiInfrastructure.Problem(h, 400, "invalid_credentials", "نام کاربری یا گذرواژه نادرست است.");
        });
        auth.MapPost("/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.Ok(); }).RequireAuthorization();
        auth.MapGet("/session", async (HttpContext h, AppDbContext db, IAntiforgery xsrf) =>
        {
            if (h.User.Identity?.IsAuthenticated != true) return Results.Ok(new SessionDto(false, null, [], null));
            var uid = h.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var permissions = await db.RolePermissions.Where(g => g.Permission!.Active && db.UserRoles.Any(ur => ur.UserId == uid && ur.RoleId == g.RoleId)).Select(g => g.Permission!.Key).Distinct().ToArrayAsync();
            var token = xsrf.GetAndStoreTokens(h).RequestToken;
            return Results.Ok(new SessionDto(true, h.User.Identity.Name, permissions, token));
        }); // Documented exception: authenticated current-user permission snapshot only.

        MapEquipment(app); MapOrders(app); MapInspections(app); MapAdmin(app);
        app.MapGet("/api/options/equipment", async (AppDbContext db) => await db.Equipment.Where(x => x.IsActive).Select(x => new { value = x.Id, label = x.Code + " — " + x.Name }).ToListAsync()).RequirePermission("production-orders.create");
        app.MapGet("/api/options/production-orders", async (AppDbContext db) => await db.ProductionOrders.Select(x => new { value = x.Id, label = x.OrderNumber }).ToListAsync()).RequirePermission("inspections.create");
    }

    private static void MapEquipment(WebApplication app)
    {
        var g = app.MapGroup("/api/equipment");
        g.MapGet("/", async (string? q, int page, int pageSize, AppDbContext db) => { var x = db.Equipment.AsNoTracking().Where(x => q == null || x.Code.Contains(q) || x.Name.Contains(q)); return new PagedResult<object>(await x.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => (object)new { x.Id, x.Code, x.Name, x.ProductionLine, x.IsActive }).ToListAsync(), await x.CountAsync(), page, pageSize); }).RequirePermission("equipment.list");
        g.MapGet("/{id:int}", async (int id, AppDbContext db) => await db.Equipment.FindAsync(id) is { } x ? Results.Ok(x) : Results.NotFound()).RequirePermission("equipment.read");
        g.MapPost("/", async (Equipment x, AppDbContext db) => { x.Id = 0; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/equipment/{x.Id}", x); }).RequirePermission("equipment.create");
        g.MapPut("/{id:int}", async (int id, Equipment input, AppDbContext db) => { var x = await db.Equipment.FindAsync(id); if (x is null) return Results.NotFound(); x.Code = input.Code; x.Name = input.Name; x.ProductionLine = input.ProductionLine; x.IsActive = input.IsActive; await db.SaveChangesAsync(); return Results.Ok(x); }).RequirePermission("equipment.update");
        g.MapDelete("/{id:int}", (int id, AppDbContext db, HttpContext h) => ApiInfrastructure.DeleteSafely<Equipment>(id, db, h)).RequirePermission("equipment.delete");
    }

    private static void MapOrders(WebApplication app)
    {
        var g = app.MapGroup("/api/production-orders");
        g.MapGet("/", async (string? q, int page, int pageSize, AppDbContext db) => { var x = db.ProductionOrders.AsNoTracking().Where(x => q == null || x.OrderNumber.Contains(q)); return new PagedResult<object>(await x.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => (object)new { x.Id, x.OrderNumber, Equipment = x.Equipment!.Name, x.PlannedQuantity, x.StartDate, x.Status }).ToListAsync(), await x.CountAsync(), page, pageSize); }).RequirePermission("production-orders.list");
        g.MapGet("/{id:int}", async (int id, AppDbContext db) => await db.ProductionOrders.Where(x => x.Id == id).Select(x => new { x.Id, x.OrderNumber, x.EquipmentId, Equipment = x.Equipment!.Name, x.PlannedQuantity, x.StartDate, x.Status }).FirstOrDefaultAsync() is { } x ? Results.Ok(x) : Results.NotFound()).RequirePermission("production-orders.read");
        g.MapPost("/", async (ProductionOrder x, AppDbContext db) => { x.Id = 0; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/production-orders/{x.Id}", x); }).RequirePermission("production-orders.create");
        g.MapPut("/{id:int}", async (int id, ProductionOrder input, AppDbContext db) => { var x = await db.ProductionOrders.FindAsync(id); if (x is null) return Results.NotFound(); x.OrderNumber = input.OrderNumber; x.EquipmentId = input.EquipmentId; x.PlannedQuantity = input.PlannedQuantity; x.StartDate = input.StartDate; x.Status = input.Status; await db.SaveChangesAsync(); return Results.Ok(x); }).RequirePermission("production-orders.update");
        g.MapDelete("/{id:int}", (int id, AppDbContext db, HttpContext h) => ApiInfrastructure.DeleteSafely<ProductionOrder>(id, db, h)).RequirePermission("production-orders.delete");
    }

    private static void MapInspections(WebApplication app)
    {
        var g = app.MapGroup("/api/inspections");
        g.MapGet("/", async (string? q, int page, int pageSize, AppDbContext db) => { var x = db.Inspections.AsNoTracking().Where(x => q == null || x.InspectionNumber.Contains(q)); return new PagedResult<object>(await x.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => (object)new { x.Id, x.InspectionNumber, OrderNumber = x.ProductionOrder!.OrderNumber, x.InspectionDate, x.Result, x.IsApproved }).ToListAsync(), await x.CountAsync(), page, pageSize); }).RequirePermission("inspections.list");
        g.MapGet("/{id:int}", async (int id, AppDbContext db) => await db.Inspections.Where(x => x.Id == id).Select(x => new { x.Id, x.InspectionNumber, x.ProductionOrderId, OrderNumber = x.ProductionOrder!.OrderNumber, x.InspectionDate, x.Result, x.Notes, x.IsApproved }).FirstOrDefaultAsync() is { } x ? Results.Ok(x) : Results.NotFound()).RequirePermission("inspections.read");
        g.MapPost("/", async (Inspection x, AppDbContext db) => { x.Id = 0; x.IsApproved = false; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/inspections/{x.Id}", x); }).RequirePermission("inspections.create");
        g.MapPut("/{id:int}", async (int id, Inspection input, AppDbContext db) => { var x = await db.Inspections.FindAsync(id); if (x is null) return Results.NotFound(); x.InspectionNumber = input.InspectionNumber; x.ProductionOrderId = input.ProductionOrderId; x.InspectionDate = input.InspectionDate; x.Result = input.Result; x.Notes = input.Notes; await db.SaveChangesAsync(); return Results.Ok(x); }).RequirePermission("inspections.update");
        g.MapDelete("/{id:int}", (int id, AppDbContext db, HttpContext h) => ApiInfrastructure.DeleteSafely<Inspection>(id, db, h)).RequirePermission("inspections.delete");
        g.MapPost("/{id:int}/approve", async (int id, AppDbContext db, HttpContext h) => { var x = await db.Inspections.FindAsync(id); if (x is null) return Results.NotFound(); if (x.IsApproved) return ApiInfrastructure.Problem(h, 409, "already_approved", "این بازرسی قبلاً تأیید شده است."); x.IsApproved = true; db.ApprovalHistories.Add(new ApprovalHistory { InspectionId = id, ApprovedAt = DateTimeOffset.UtcNow, ApprovedBy = h.User.Identity!.Name! }); await db.SaveChangesAsync(); return Results.Ok(); }).RequirePermission("inspections.approve");
        g.MapGet("/{id:int}/approval-history", async (int id, AppDbContext db) => await db.ApprovalHistories.Where(x => x.InspectionId == id).Select(x => new { x.ApprovedBy, x.ApprovedAt }).ToListAsync()).RequirePermission("inspections.approval-history.read");
    }

    private static void MapAdmin(WebApplication app)
    {
        var g = app.MapGroup("/api/admin");
        g.MapGet("/permissions", async (string? role, AppDbContext db) => { var roleId = await db.Roles.Where(x => x.Name == role).Select(x => x.Id).FirstOrDefaultAsync(); return await db.PermissionCatalog.OrderBy(x => x.Module).ThenBy(x => x.Key).Select(x => new PermissionDto(x.Key, x.PersianTitle, x.Module, x.Kind, x.Active, roleId != null && db.RolePermissions.Any(g => g.RoleId == roleId && g.PermissionId == x.Id), x.RelatedUi)).ToListAsync(); }).RequirePermission("admin.permissions.read");
        g.MapGet("/roles", async (AppDbContext db) => await db.Roles.Select(x => x.Name!).OrderBy(x => x).ToArrayAsync()).RequirePermission("admin.permissions.read");
        // PERMISSION-GRANT
        g.MapPost("/grants", async (GrantRequest request, AppDbContext db, ILoggerFactory logs, HttpContext h) => { var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == request.Role); var permission = await db.PermissionCatalog.SingleOrDefaultAsync(x => x.Key == request.PermissionKey && x.Active); if (role is null || permission is null) return Results.BadRequest(); var row = await db.RolePermissions.SingleOrDefaultAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id); if (request.Granted && row is null) db.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id }); if (!request.Granted && row is not null) db.Remove(row); await db.SaveChangesAsync(); logs.CreateLogger("Audit").LogInformation("Grant changed by {User}: role {Role}, permission {Permission}, granted {Granted}", h.User.Identity!.Name, request.Role, request.PermissionKey, request.Granted); return Results.Ok(); }).RequirePermission("admin.permissions.grant");
    }
}
