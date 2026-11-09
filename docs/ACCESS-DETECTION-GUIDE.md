# راهنمای کشف و اعمال دسترسی‌ها

این راهنما توضیح می‌دهد دسترسی‌ها چگونه از تعریف قابلیت تا API و رابط Blazor جریان پیدا می‌کنند و برای افزودن کنترل یا فرم جدید چه تغییراتی لازم است. نام کلاس‌ها، کلیدها و کدها انگلیسی هستند؛ متن قابل مشاهده برای کاربر فارسی است.

## نمای کلی جریان دسترسی

```text
IFeatureModule / ModuleDefinition
          │
          ▼
FeatureCatalog.Discover()
          │  تولید کلیدهای پایدار
          ▼
PermissionCatalogSynchronizer.SyncAsync()
          │  ثبت در SQLite و حفظ grantهای موجود
          ▼
RolePermission ───────────────┐
          │                   │
          ▼                   ▼
PermissionAuthorizationHandler   GET /api/auth/session
          │                   │
          ▼                   ▼
API اجازه/رد می‌کند          SessionService در مرورگر
                              │
                              ▼
                    منو، دکمه، زبانه و route
```

اصل امنیتی مهم: مخفی‌کردن دکمه در مرورگر فقط برای تجربهٔ کاربری است. تصمیم نهایی همیشه در سرور گرفته می‌شود. کاربر می‌تواند HTML یا JavaScript را تغییر دهد یا مستقیماً API را فراخوانی کند، اما بدون grant فعال، سرور درخواست را با `403` رد می‌کند.

## ۱. تعریف قابلیت در ماژول

تعریف‌های مشترک در فایل زیر قرار دارند:

`src/PipeMillPermissions/PipeMillPermissions.Client/Shared/Contracts.cs`

هر فرم تجاری یک کلاس پیاده‌ساز `IFeatureModule` دارد. نمونهٔ بازرسی:

```csharp
public sealed class InspectionModule : IFeatureModule
{
    public ModuleDefinition Definition => new(
        "inspections",                 // شناسهٔ پایدار منبع
        "کنترل کیفیت",                 // گروه منو
        "بازرسی‌ها",                   // عنوان جمع
        "بازرسی",                      // عنوان مفرد
        "/modules/inspections",        // route رابط
        [ /* fields */ ],
        [
            new("approve-button", "تأیید بازرسی", "approve", "کنترل"),
            new("approval-history-tab", "سوابق تأیید", "approval-history.read", "زبانه")
        ]);
}
```

پنج عملیات CRUD به‌صورت خودکار از هر تعریف ساخته می‌شوند:

```text
<resource>.list
<resource>.read
<resource>.create
<resource>.update
<resource>.delete
```

برای مثال `resourceId` برابر `inspections` کلیدهای `inspections.list` تا `inspections.delete` را می‌سازد. هر `ProtectedElement` نیز کلیدی از ترکیب resource و operation تولید می‌کند؛ مانند `inspections.approve`.

شناسهٔ resource و نام operation قرارداد پایدار سیستم هستند. پس از انتشار آن‌ها را صرفاً برای تغییر عنوان یا route عوض نکنید، چون کلید جدید به‌عنوان دسترسی جدید و بدون grant ظاهر می‌شود.

## ۲. کشف خودکار در سرور

نقطهٔ اصلی کشف:

`Permissions/PermissionSystem.cs` → `FeatureCatalog.Discover`

این متد با reflection همهٔ کلاس‌های غیر abstract پیاده‌ساز `IFeatureModule` را از assemblyهای تنظیم‌شده پیدا می‌کند. سپس:

1. پنج عملیات CRUD را اضافه می‌کند.
2. عملیات کنترل‌ها و زبانه‌های محافظت‌شده را اضافه می‌کند.
3. با `DeriveKey(resourceId, operation)` کلید پایدار می‌سازد.
4. در صورت وجود دو کلید یکسان، startup را متوقف می‌کند.

کاتالوگ قبل از پذیرش ترافیک در `Program.cs` ساخته می‌شود؛ بنابراین لازم نیست کاربر ابتدا صفحه‌ای را باز کند تا کلیدهای آن کشف شوند.

## ۳. همگام‌سازی با SQLite

نقطهٔ اصلی:

`PermissionCatalogSynchronizer.SyncAsync`

در شروع برنامه، قابلیت‌های کشف‌شده در جدول `PermissionCatalog` upsert می‌شوند. رفتار همگام‌سازی:

- کلید جدید: رکورد فعال جدید، بدون grant پیش‌فرض.
- عنوان یا route جدید با همان کلید: همان رکورد به‌روزرسانی و grant حفظ می‌شود.
- کلید حذف‌شده از کد: رکورد حذف نمی‌شود؛ `Active = false` می‌گیرد.
- قابلیت غیرفعال: حتی با وجود grant قدیمی رد می‌شود.
- اجرای چندباره: رکورد تکراری ایجاد نمی‌کند.

ارتباط نقش و قابلیت در جدول `RolePermission` ذخیره می‌شود. تغییرات مدیر از endpoint زیر عبور می‌کند:

`POST /api/admin/grants`

## ۴. تصمیم دسترسی در سرور

هر endpoint تجاری یا مدیریتی با `RequirePermission` محافظت می‌شود:

```csharp
app.MapPost("/{id:int}/approve", ApproveInspection)
   .RequirePermission("inspections.approve");
```

`RequirePermission` یک `PermissionRequirement` و metadata کلید را به endpoint متصل می‌کند. سپس `PermissionAuthorizationHandler` در هر درخواست:

1. شناسهٔ کاربر را از principal کوکی می‌خواند.
2. نقش‌های فعلی کاربر را از دیتابیس بررسی می‌کند.
3. grant نقش برای همان کلید را بررسی می‌کند.
4. فعال‌بودن قابلیت در catalog را بررسی می‌کند.
5. فقط در صورت برقرار بودن همهٔ شرایط requirement را موفق می‌کند.

grant در claim دائمی کوکی ذخیره نشده است. به همین دلیل لغو دسترسی در نشست فعال، از درخواست بعدی اعمال می‌شود.

### پاسخ‌های امنیتی

- کاربر ناشناس: `401` با ProblemDetails و کد `authentication_required`، بدون redirect HTML.
- کاربر واردشده بدون grant: `403` با کد `permission_denied`.
- رکورد ناموجود: `404`.
- ورودی نامعتبر: `400`.
- تعارض رابطهٔ دیتابیس: `409`.

## ۵. تشخیص دسترسی در مرورگر و UI

مرورگر از endpoint زیر snapshot دسترسی‌های جاری را می‌گیرد:

```http
GET /api/auth/session
```

پاسخ شامل وضعیت ورود، نام کاربر، کلیدهای مؤثر و antiforgery token است. `SessionService` آن را نگهداری می‌کند:

```csharp
await Session.RefreshAsync();

if (Session.Can("inspections.approve"))
{
    // نمایش کنترل مجاز
}
```

کاربردها:

- `NavMenu.razor`: منو فقط با مجوز `.list` نمایش داده می‌شود.
- `ModulePage.razor`: دکمه‌های ایجاد، ویرایش و حذف با کلید متناظر نمایش داده می‌شوند.
- زبانهٔ سوابق تأیید: فقط با `inspections.approval-history.read`.
- دکمهٔ تأیید: فقط با `inspections.approve`.
- صفحهٔ مدیریت: فقط با `admin.permissions.read`.

`SendMutationAsync` antiforgery token را در هدر `X-XSRF-TOKEN` می‌فرستد. اگر پاسخ `403` باشد، snapshot را دوباره دریافت می‌کند تا کنترل لغوشده از UI حذف شود.

نکته: `Session.Can(...)` قابل اعتماد امنیتی نیست؛ کاربر می‌تواند آن را در مرورگر دور بزند. endpoint سرور باید همیشه همان کلید را enforce کند.

## افزودن یک کنترل محافظت‌شدهٔ جدید

فرض کنید می‌خواهیم دکمهٔ «ارسال برای بازکاری» به بازرسی اضافه کنیم.

### مرحله ۱: اعلام قابلیت در تعریف ماژول

در `InspectionModule` یک `ProtectedElement` اضافه کنید:

```csharp
new(
    "send-to-rework-button",
    "ارسال برای بازکاری",
    "send-to-rework",
    "کنترل")
```

کلید جدید حاصل:

```text
inspections.send-to-rework
```

### مرحله ۲: endpoint مستقل و محافظت‌شده

در `Infrastructure/EndpointMappings.cs`:

```csharp
g.MapPost("/{id:int}/send-to-rework", async (
    int id,
    AppDbContext db) =>
{
    var inspection = await db.Inspections.FindAsync(id);
    if (inspection is null)
        return Results.NotFound();

    // Only this operation may make the protected state change.
    inspection.Result = "نیازمند بازکاری";
    await db.SaveChangesAsync();
    return Results.Ok();
})
.RequirePermission("inspections.send-to-rework");
```

از generic update برای تغییر وضعیت مستقل استفاده نکنید. DTO و mapping ویرایش عمومی نباید فیلد محافظت‌شده را قبول یا کپی کنند.

### مرحله ۳: نمایش کنترل در UI

در `ModulePage.razor`:

```razor
@if (Session.Can("inspections.send-to-rework"))
{
    <button class="btn btn-secondary" @onclick="SendToRework">
        ارسال برای بازکاری
    </button>
}
```

و mutation را از `SessionService` بفرستید:

```csharp
var response = await Session.SendMutationAsync(
    HttpMethod.Post,
    $"api/inspections/{id}/send-to-rework");
```

### مرحله ۴: grant مدیر

برنامه را restart کنید. کلید جدید در «مدیریت دسترسی‌ها» فعال ولی بدون نقش نمایش داده می‌شود. مدیر آن را برای نقش مورد نظر اعطا می‌کند.

### مرحله ۵: آزمون ضروری

- بدون grant، دکمه دیده نمی‌شود.
- درخواست مستقیم بدون grant، `403` می‌گیرد و داده تغییر نمی‌کند.
- با grant و antiforgery token معتبر، عملیات موفق است.
- پس از revoke در همان نشست، درخواست بعدی رد و دکمه حذف می‌شود.

## افزودن زبانهٔ محافظت‌شده

برای زبانهٔ «نتایج آزمایشگاه»:

```csharp
new(
    "laboratory-results-tab",
    "نتایج آزمایشگاه",
    "laboratory-results.read",
    "زبانه")
```

کلید حاصل `inspections.laboratory-results.read` است. دادهٔ زبانه را در payload عمومی list/detail قرار ندهید. یک API مستقل بسازید:

```csharp
g.MapGet("/{id:int}/laboratory-results", LoadLaboratoryResults)
 .RequirePermission("inspections.laboratory-results.read");
```

در UI فقط در صورت `Session.Can(...)` زبانه را نمایش و فقط پس از انتخاب آن API مستقل را فراخوانی کنید.

## افزودن یک فرم تجاری جدید

مثال: فرم «درخواست‌های تعمیرات» با resource پایدار `maintenance-requests`.

### ۱. مدل و رابطه‌های دیتابیس

در `Data/AppDbContext.cs` entity و `DbSet` را اضافه کنید:

```csharp
public sealed class MaintenanceRequest
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = "";
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public DateOnly RequestedDate { get; set; }
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
}

public DbSet<MaintenanceRequest> MaintenanceRequests =>
    Set<MaintenanceRequest>();
```

رابطه و delete behavior را در `OnModelCreating` تنظیم و migration ایجاد کنید.

### ۲. تعریف ماژول

در `Contracts.cs`:

```csharp
public sealed class MaintenanceRequestModule : IFeatureModule
{
    public ModuleDefinition Definition => new(
        "maintenance-requests",
        "نگهداری و تعمیرات",
        "درخواست‌های تعمیرات",
        "درخواست تعمیرات",
        "/modules/maintenance-requests",
        [
            new("requestNumber", "شماره درخواست", FieldKind.Text, true),
            new("equipmentId", "تجهیز", FieldKind.Select, true,
                "/api/options/equipment"),
            new("requestedDate", "تاریخ درخواست", FieldKind.Date, true),
            new("priority", "اولویت", FieldKind.Select, true),
            new("status", "وضعیت", FieldKind.Select, true)
        ],
        [
            new("close-button", "بستن درخواست", "close", "کنترل"),
            new("work-log-tab", "گزارش کار", "work-log.read", "زبانه")
        ]);
}
```

پس از discovery این کلیدها ظاهر می‌شوند:

```text
maintenance-requests.list
maintenance-requests.read
maintenance-requests.create
maintenance-requests.update
maintenance-requests.delete
maintenance-requests.close
maintenance-requests.work-log.read
```

### ۳. ثبت تعریف برای رندر فعلی Client

کشف سرور reflection-based و خودکار است. با این حال، پیاده‌سازی فعلی Client برای رندر منو و `ModulePage` از آرایهٔ `ClientFeatureCatalog.Modules` استفاده می‌کند. کلاس جدید را به آن آرایه اضافه کنید:

```csharp
public static readonly ModuleDefinition[] Modules =
[
    new EquipmentModule().Definition,
    new ProductionOrderModule().Definition,
    new InspectionModule().Definition,
    new MaintenanceRequestModule().Definition
];
```

این محدودیت فعلی UI است؛ catalog امنیتی سرور همچنان از reflection ساخته می‌شود و این آرایه منبع تصمیم امنیتی نیست.

### ۴. APIهای CRUD

در `EndpointMappings.cs` یک mapper برای list/read/create/update/delete اضافه و هر مسیر را با کلید متناظر محافظت کنید. از request DTO اختصاصی استفاده کنید و entity را مستقیماً به‌عنوان مدرک اختیار مرورگر نپذیرید.

```csharp
g.MapGet("/", ListMaintenance)
 .RequirePermission("maintenance-requests.list");

g.MapGet("/{id:int}", ReadMaintenance)
 .RequirePermission("maintenance-requests.read");

g.MapPost("/", CreateMaintenance)
 .RequirePermission("maintenance-requests.create");

g.MapPut("/{id:int}", UpdateMaintenance)
 .RequirePermission("maintenance-requests.update");

g.MapDelete("/{id:int}", DeleteMaintenance)
 .RequirePermission("maintenance-requests.delete");
```

### ۵. UI و داده‌های انتخابی

`ModulePage.razor` تعریف فیلدها را رندر می‌کند. برای dropdownها API گزینه بسازید و آن را نیز با مجوز مناسب محافظت کنید. داده‌ای که به‌طور مستقل محافظت می‌شود نباید در گزینه‌ها، list یا detail عمومی نشت کند.

### ۶. migration و آزمون

پس از تغییر مدل:

```bash
dotnet ef migrations add AddMaintenanceRequests \
  --project src/PipeMillPermissions/PipeMillPermissions/PipeMillPermissions.csproj

dotnet test PipeMillPermissions.slnx
```

حداقل آزمون‌ها:

- ماژول بدون تغییر registry سرور کشف شود.
- پنج CRUD و قابلیت‌های اضافی قبل از بازشدن صفحه در catalog باشند.
- کلیدهای جدید grant پیش‌فرض نداشته باشند.
- list/detail فقط DTO مجاز را برگردانند.
- `401` ناشناس و `403` کاربر بدون grant تأیید شود.
- CRUD مجاز، validation، `404` و تعارض `409` آزموده شوند.

## چک‌لیست هر قابلیت جدید

- [ ] resource ID و operation پایدار و انگلیسی است.
- [ ] عنوان قابل مشاهده فارسی است.
- [ ] قابلیت در `ModuleDefinition` اعلام شده است.
- [ ] endpoint همان کلید را با `RequirePermission` enforce می‌کند.
- [ ] mutation با antiforgery token ارسال می‌شود.
- [ ] UI از `Session.Can` فقط برای نمایش استفاده می‌کند.
- [ ] payload عمومی دادهٔ محافظت‌شده را افشا نمی‌کند.
- [ ] مدیر می‌تواند کلید را در catalog ببیند و grant کند.
- [ ] تست مستقیم API ثابت می‌کند دورزدن UI ممکن نیست.
- [ ] revoke در نشست فعال در درخواست بعدی اعمال می‌شود.

## مسیرهای اصلی کد

| مسئولیت | فایل/متد |
|---|---|
| تعریف ماژول‌ها | `Client/Shared/Contracts.cs` |
| کشف و تولید کلید | `Permissions/PermissionSystem.cs` → `FeatureCatalog` |
| sync کاتالوگ | همان فایل → `PermissionCatalogSynchronizer` |
| تصمیم سرور | همان فایل → `PermissionAuthorizationHandler` |
| اتصال endpoint | همان فایل → `RequirePermission` |
| endpointهای تجاری و مدیریت | `Infrastructure/EndpointMappings.cs` |
| snapshot مرورگر | `Client/Services/SessionService.cs` |
| منو | `Client/Layout/NavMenu.razor` |
| فرم و کنترل‌های CRUD | `Client/Pages/ModulePage.razor` |
| grant نقش‌ها | `Client/Pages/AdminPermissions.razor` |

