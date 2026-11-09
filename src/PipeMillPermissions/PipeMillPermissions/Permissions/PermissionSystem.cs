using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PipeMillPermissions.Client.Shared;
using PipeMillPermissions.Data;

namespace PipeMillPermissions.Permissions;

public sealed record CatalogCapability(string Key, string Title, string Module, string Kind, string RelatedUi);

public sealed class FeatureCatalog
{
    public IReadOnlyList<ModuleDefinition> Modules { get; }
    public IReadOnlyList<CatalogCapability> Capabilities { get; }

    private FeatureCatalog(IReadOnlyList<ModuleDefinition> modules, IReadOnlyList<CatalogCapability> capabilities) { Modules = modules; Capabilities = capabilities; }

    // PERMISSION-DISCOVERY
    public static FeatureCatalog Discover(params Assembly[] assemblies)
    {
        var modules = assemblies.SelectMany(a => a.DefinedTypes)
            .Where(t => !t.IsAbstract && typeof(IFeatureModule).IsAssignableFrom(t.AsType()))
            .Select(t => ((IFeatureModule)Activator.CreateInstance(t.AsType())!).Definition).OrderBy(x => x.Route).ToArray();
        var capabilities = modules.SelectMany(m => m.Operations.Select(o => new CatalogCapability(
            DeriveKey(m.ResourceId, o.Name), o.PersianTitle, m.Title, o.Kind,
            m.ProtectedElements.FirstOrDefault(x => x.Operation == o.Name)?.Label ?? $"فرم {m.SingularTitle}"))).ToList();
        capabilities.AddRange(AdminCapabilities.All);
        var duplicate = capabilities.GroupBy(x => x.Key, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Conflicting permission key: {duplicate.Key}");
        return new FeatureCatalog(modules, capabilities);
    }

    // PERMISSION-KEY
    public static string DeriveKey(string resourceId, string operation) => $"{resourceId}.{operation}";
}

public static class AdminCapabilities
{
    public static readonly CatalogCapability[] All =
    [new("admin.permissions.read", "مشاهده فهرست دسترسی‌ها", "مدیریت دسترسی‌ها", "مدیریت", "صفحه مدیریت دسترسی‌ها"),
     new("admin.permissions.grant", "اعطا یا لغو دسترسی", "مدیریت دسترسی‌ها", "مدیریت", "کلیدهای اعطا و لغو"),
     new("admin.roles.assign", "تخصیص نقش", "مدیریت دسترسی‌ها", "مدیریت", "تخصیص نقش کاربران")];
}

public sealed class PermissionCatalogSynchronizer(AppDbContext db, FeatureCatalog catalog)
{
    // PERMISSION-SYNC
    public async Task SyncAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.PermissionCatalog.ToDictionaryAsync(x => x.Key, ct);
        foreach (var item in existing.Values) item.Active = false;
        foreach (var c in catalog.Capabilities)
        {
            if (!existing.TryGetValue(c.Key, out var row)) { row = new PermissionCatalogItem { Key = c.Key }; db.Add(row); }
            row.PersianTitle = c.Title; row.Module = c.Module; row.Kind = c.Kind; row.RelatedUi = c.RelatedUi; row.Active = true;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}

public sealed record PermissionRequirement(string Key) : IAuthorizationRequirement;
public sealed class PermissionAuthorizationHandler(AppDbContext db, ILogger<PermissionAuthorizationHandler> log) : AuthorizationHandler<PermissionRequirement>
{
    // PERMISSION-CHECK
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return;
        var granted = await db.RolePermissions.AnyAsync(g => g.Permission!.Key == requirement.Key && g.Permission.Active &&
            db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == g.RoleId));
        if (granted) context.Succeed(requirement); else log.LogWarning("Permission denied. User {UserId}, key {PermissionKey}", userId, requirement.Key);
    }
}

public sealed record PermissionMetadata(string Key);

public static class PermissionEndpointConvention
{
    // PERMISSION-API-MAP
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder endpoint, string key) =>
        endpoint.WithMetadata(new PermissionMetadata(key)).RequireAuthorization(new AuthorizationPolicyBuilder().AddRequirements(new PermissionRequirement(key)).Build());
}
