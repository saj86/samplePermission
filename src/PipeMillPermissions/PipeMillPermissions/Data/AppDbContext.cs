using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PipeMillPermissions.Data;

public sealed class AppUser : IdentityUser { public string DisplayName { get; set; } = ""; }
public sealed class Equipment { public int Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string ProductionLine { get; set; } = ""; public bool IsActive { get; set; } }
public sealed class ProductionOrder { public int Id { get; set; } public string OrderNumber { get; set; } = ""; public int EquipmentId { get; set; } public Equipment? Equipment { get; set; } public int PlannedQuantity { get; set; } public DateOnly StartDate { get; set; } public string Status { get; set; } = ""; }
public sealed class Inspection { public int Id { get; set; } public string InspectionNumber { get; set; } = ""; public int ProductionOrderId { get; set; } public ProductionOrder? ProductionOrder { get; set; } public DateOnly InspectionDate { get; set; } public string Result { get; set; } = ""; public string? Notes { get; set; } public bool IsApproved { get; set; } }
public sealed class ApprovalHistory { public int Id { get; set; } public int InspectionId { get; set; } public Inspection? Inspection { get; set; } public string ApprovedBy { get; set; } = ""; public DateTimeOffset ApprovedAt { get; set; } }
public sealed class PermissionCatalogItem { public int Id { get; set; } public string Key { get; set; } = ""; public string PersianTitle { get; set; } = ""; public string Module { get; set; } = ""; public string Kind { get; set; } = ""; public string RelatedUi { get; set; } = ""; public bool Active { get; set; } }
public sealed class RolePermission { public int Id { get; set; } public string RoleId { get; set; } = ""; public IdentityRole? Role { get; set; } public int PermissionId { get; set; } public PermissionCatalogItem? Permission { get; set; } }

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    public DbSet<PermissionCatalogItem> PermissionCatalog => Set<PermissionCatalogItem>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<PermissionCatalogItem>().HasIndex(x => x.Key).IsUnique();
        b.Entity<RolePermission>().HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        b.Entity<RolePermission>().HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<RolePermission>().HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductionOrder>().HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Inspection>().HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
