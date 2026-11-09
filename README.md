# سامانه نمونه مدیریت دسترسی کارخانه لوله

این پروژه یک نمونهٔ اجرایی .NET 10 است: رابط Blazor WebAssembly و API از یک مبدأ، Identity با کوکی امن، EF Core و SQLite. رابط کاملاً فارسی و راست‌به‌چپ است و مجوزها از تعریف واحد هر ماژول کشف می‌شوند.

## راه‌اندازی سریع

پیش‌نیاز: .NET SDK 10. گذرواژه در سورس نیست. برای محیط توسعه یک گذرواژهٔ قوی (حداقل ۱۰ نویسه، شامل بزرگ/کوچک/عدد/نماد) تعیین کنید:

```bash
export PIPEMILL_DEMO_PASSWORD='Change-Me_1405!'
dotnet restore PipeMillPermissions.slnx
dotnet run --project src/PipeMillPermissions/PipeMillPermissions/PipeMillPermissions.csproj
```

سپس آدرس درج‌شده در ترمینال را باز کنید. حساب‌ها: `admin`، `viewer` و `inspector` با همان گذرواژهٔ محیطی. ایجاد این حساب‌ها فقط در Development انجام می‌شود؛ ثبت‌نام عمومی وجود ندارد. مدیر اولیه همهٔ مجوزهای فعال را هنگام seed دریافت می‌کند. اگر متغیر تنظیم نشود، هیچ حساب نمایشی ساخته نمی‌شود.

آزمون‌ها: `dotnet test PipeMillPermissions.slnx`. فایل SQLite در اجرای نخست ساخته و catalog در تراکنش همگام می‌شود. حذف کلید، آن را غیرفعال می‌کند و grant آن حفظ ولی غیرقابل استفاده می‌ماند.

## سناریوی دو کاربر

۱. با `viewer` وارد شوید؛ فهرست‌ها قابل مشاهده‌اند ولی دکمهٔ حذف/ویرایش دیده نمی‌شود و URL/API مستقیم نیز 403 می‌دهد. ۲. در مرورگر دیگر با `admin` وارد صفحهٔ «مدیریت دسترسی‌ها» شوید و `inspections.approve` را برای Inspector فعال کنید. ۳. با `inspector` بازرسی را تأیید کنید. ۴. مدیر grant را لغو کند؛ درخواست بعدی Inspector با 403 رد و snapshot رابط تازه می‌شود و دکمه حذف می‌گردد.

## درخواست مستقیم و ضد جعل

```bash
# ورود و نگهداری کوکی (گذرواژه را از محیط بخوانید)
curl -c /tmp/pipe.cookies -H 'Content-Type: application/json' \
  -d "{\"userName\":\"viewer\",\"password\":\"$PIPEMILL_DEMO_PASSWORD\"}" \
  http://localhost:5000/api/auth/login

# snapshot شامل antiforgeryToken
curl -b /tmp/pipe.cookies http://localhost:5000/api/auth/session

# تلاش دور زدن: توکن snapshot را در هدر X-XSRF-TOKEN قرار دهید؛
# viewer با وجود نشست و توکن معتبر همچنان 403 می‌گیرد.
curl -i -X DELETE -b /tmp/pipe.cookies -H "X-XSRF-TOKEN: $TOKEN" \
  http://localhost:5000/api/equipment/1
```

## نقشه کد دسترسی‌ها

| نشانگر | مسیر | کلاس/متد | مسئولیت |
|---|---|---|---|
| `PERMISSION-DISCOVERY` | `src/PipeMillPermissions/PipeMillPermissions/Permissions/PermissionSystem.cs` | `FeatureCatalog.Discover` | کشف خودکار ماژول‌ها و عملیات |
| `PERMISSION-KEY` | همان فایل | `FeatureCatalog.DeriveKey` | تولید کلید پایدار |
| `PERMISSION-SYNC` | همان فایل | `PermissionCatalogSynchronizer.SyncAsync` | upsert تراکنشی و غیرفعال‌سازی کلید حذف‌شده |
| `PERMISSION-API-MAP` | همان فایل | `RequirePermission` | اتصال requirement به endpoint |
| `PERMISSION-CHECK` | همان فایل | `PermissionAuthorizationHandler.HandleRequirementAsync` | تصمیم سرور با grant فعال جاری |
| `PERMISSION-UI` | `src/PipeMillPermissions/PipeMillPermissions.Client/Services/SessionService.cs` | `CanAccess` | نمایش route/control مجاز |
| `PERMISSION-ERROR` | `src/PipeMillPermissions/PipeMillPermissions/Infrastructure/ApiInfrastructure.cs` | `ApiStatusHandler.HandleAsync` | ProblemDetails برای 401/403 |
| `PERMISSION-GRANT` | `src/PipeMillPermissions/PipeMillPermissions/Infrastructure/EndpointMappings.cs` | `POST /api/admin/grants` | ثبت اعطا/لغو و audit |

## ردگیری «تأیید بازرسی»

تعریف `InspectionModule` عملیات `approve` را اعلام می‌کند؛ Discovery کلید `inspections.approve` را می‌سازد؛ Sync آن را در catalog قرار می‌دهد؛ مدیر از صفحهٔ grant آن را به نقش می‌دهد؛ `ModulePage.razor` دکمه را فقط با همان کلید نشان می‌دهد؛ endpoint مستقل `POST /api/inspections/{id}/approve` همان requirement را دارد؛ handler در هر درخواست grant فعال را از SQLite می‌خواند. ویرایش عمومی عمداً `IsApproved` را کپی نمی‌کند.

## افزودن فرم چهارم

در `Contracts.cs` یک کلاس پیاده‌ساز `IFeatureModule` بسازید (مثلاً resource برابر `maintenance`) و فیلدها را تعریف کنید. بدون registry یا seed مرکزی، پنج کلید `maintenance.list/read/create/update/delete` ظاهر می‌شود. با افزودن protected element مانند operation برابر `audit.read`، کلید `maintenance.audit.read` نیز ساخته می‌شود. endpoint را با `RequirePermission` محافظت و صفحه را از همان تعریف رندر کنید؛ سپس مدیر کلید جدید را در «مدیریت دسترسی‌ها» برای نقش مورد نظر فعال می‌کند. کلیدهای تازه پیش‌فرض بدون grant هستند.

## نکات امنیتی

payload فهرست/جزئیات بازرسی تاریخچهٔ تأیید را ندارد و تاریخچه endpoint مستقل دارد. پاسخ API برای ناشناس 401 و برای فاقد مجوز 403 بدون redirect است. تصمیم مجوز همیشه روی سرور و با principal کوکی انجام می‌شود. دادهٔ حساس، کوکی و گذرواژه log نمی‌شود. کلید ناشناخته یا غیرفعال fail-closed است.
