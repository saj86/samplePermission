using System.ComponentModel.DataAnnotations;

namespace PipeMillPermissions.Client.Shared;

public enum FieldKind { Text, Number, Date, Checkbox, Select, Multiline }
public sealed record FieldDefinition(string Name, string Label, FieldKind Kind, bool Required = false, string? OptionsEndpoint = null);
public sealed record ProtectedElement(string Id, string Label, string Operation, string Kind);
public sealed record OperationDefinition(string Name, string PersianTitle, string Kind);
public sealed record ModuleDefinition(string ResourceId, string MenuGroup, string Title, string SingularTitle, string Route,
    IReadOnlyList<FieldDefinition> Fields, IReadOnlyList<ProtectedElement> ProtectedElements)
{
    public IReadOnlyList<OperationDefinition> Operations =>
    [
        new("list", $"فهرست {Title}", "عملیات"), new("read", $"مشاهده {SingularTitle}", "عملیات"),
        new("create", $"ایجاد {SingularTitle}", "عملیات"), new("update", $"ویرایش {SingularTitle}", "عملیات"),
        new("delete", $"حذف {SingularTitle}", "عملیات"),
        .. ProtectedElements.Select(x => new OperationDefinition(x.Operation, x.Label, x.Kind))
    ];
}

public interface IFeatureModule { ModuleDefinition Definition { get; } }
public static class ClientFeatureCatalog
{
    public static readonly ModuleDefinition[] Modules = [new EquipmentModule().Definition, new ProductionOrderModule().Definition, new InspectionModule().Definition];
}

public sealed class EquipmentModule : IFeatureModule
{
    public ModuleDefinition Definition => new("equipment", "اطلاعات پایه", "تجهیزات", "تجهیز", "/modules/equipment",
    [new("code", "کد تجهیز", FieldKind.Text, true), new("name", "نام", FieldKind.Text, true),
     new("productionLine", "خط تولید", FieldKind.Select, true), new("isActive", "فعال", FieldKind.Checkbox)], []);
}

public sealed class ProductionOrderModule : IFeatureModule
{
    public ModuleDefinition Definition => new("production-orders", "تولید", "سفارش‌های تولید", "سفارش تولید", "/modules/production-orders",
    [new("orderNumber", "شماره سفارش", FieldKind.Text, true), new("equipmentId", "تجهیز", FieldKind.Select, true, "/api/options/equipment"),
     new("plannedQuantity", "تعداد برنامه‌ریزی‌شده", FieldKind.Number, true), new("startDate", "تاریخ شروع", FieldKind.Date, true),
     new("status", "وضعیت", FieldKind.Select, true)], []);
}

public sealed class InspectionModule : IFeatureModule
{
    public ModuleDefinition Definition => new("inspections", "کنترل کیفیت", "بازرسی‌ها", "بازرسی", "/modules/inspections",
    [new("inspectionNumber", "شماره بازرسی", FieldKind.Text, true), new("productionOrderId", "سفارش تولید", FieldKind.Select, true, "/api/options/production-orders"),
     new("inspectionDate", "تاریخ", FieldKind.Date, true), new("result", "نتیجه", FieldKind.Select, true), new("notes", "توضیحات", FieldKind.Multiline)],
    [new("approve-button", "تأیید بازرسی", "approve", "کنترل"), new("approval-history-tab", "سوابق تأیید", "approval-history.read", "زبانه")]);
}

public sealed record PermissionDto(string Key, string Title, string Module, string Kind, bool Active, bool Granted, string RelatedUi);
public sealed record SessionDto(bool IsAuthenticated, string? DisplayName, string[] Permissions, string? AntiforgeryToken);
public sealed class LoginRequest
{
    [Required] public string UserName { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public sealed record GrantRequest(string Role, string PermissionKey, bool Granted);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record ApiError(string Code, string Message, string TraceId);
