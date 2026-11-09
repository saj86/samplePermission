# سامانه نمونه مدیریت دسترسی کارخانه لوله

این پروژه یک نمونهٔ اجرایی .NET 10 است: رابط Blazor WebAssembly و API از یک مبدأ، Identity با کوکی امن، EF Core و SQLite. رابط کاملاً فارسی و راست‌به‌چپ است و مجوزها از تعریف واحد هر ماژول کشف می‌شوند.

## راه‌اندازی سریع

پیش‌نیاز: .NET SDK 10. گذرواژه در سورس نیست. برای محیط توسعه یک گذرواژهٔ قوی (حداقل ۱۰ نویسه، شامل بزرگ/کوچک/عدد/نماد) تعیین کنید:

```bash
export PIPEMILL_DEMO_PASSWORD='Change-Me_1405!'
dotnet restore PipeMillPermissions.slnx
dotnet run --project src/PipeMillPermissions/PipeMillPermissions/PipeMillPermissions.csproj
```

سپس آدرس درج‌شده در ترمینال را باز کنید. ایجاد حساب‌های نمایشی فقط در محیط `Development` انجام می‌شود؛ ثبت‌نام عمومی وجود ندارد. اگر متغیر گذرواژه تنظیم نشود، هیچ حساب نمایشی ساخته نمی‌شود.

> گذرواژهٔ نمونهٔ بالا فقط برای اجرای محلی است. آن را در Git، README، اسکرین‌شات یا محیط واقعی استفاده نکنید. همهٔ کاربران seedشده از مقدار جاری `PIPEMILL_DEMO_PASSWORD` استفاده می‌کنند.

## کاربران Seed و سطح دسترسی

| نام کاربری | عنوان نمایشی | نقش | دسترسی اولیه | کاربرد پیشنهادی |
|---|---|---|---|---|
| `admin` | مدیر سامانه | `Administrator` | همهٔ مجوزهای فعال، شامل مدیریت grantها | مدیریت دسترسی نقش‌ها و آزمایش کامل CRUD |
| `viewer` | کاربر مشاهده‌گر | `Viewer` | فهرست و جزئیات تجهیزات، سفارش‌های تولید و بازرسی‌ها | مشاهدهٔ رفتار فقط‌خواندنی و مخفی‌شدن عملیات غیرمجاز |
| `inspector` | بازرس کیفیت | `Inspector` | فهرست، جزئیات و ویرایش بازرسی‌ها و مشاهدهٔ سوابق تأیید | کار روزمرهٔ کنترل کیفیت؛ مجوز تأیید مستقل است و مدیر باید آن را اعطا کند |

مجوزهای دقیق اولیه:

```text
Administrator: تمام کلیدهای فعال catalog

Viewer:
equipment.list, equipment.read
production-orders.list, production-orders.read
inspections.list, inspections.read

Inspector:
inspections.list, inspections.read, inspections.update
inspections.approval-history.read
```

مجوز `inspections.approve` عمداً در seed نقش Inspector وجود ندارد تا اعطا و لغو زندهٔ دسترسی قابل نمایش باشد. مدیر می‌تواند آن را از صفحهٔ «مدیریت دسترسی‌ها» فعال کند.

### ورود به سامانه

1. برنامه را با دستور بالا اجرا کنید و آدرس محلی نمایش‌داده‌شده را باز کنید.
2. از بالای صفحه «ورود / حساب کاربری» را انتخاب کنید.
3. یکی از نام‌های کاربری جدول بالا و مقدار `PIPEMILL_DEMO_PASSWORD` را وارد کنید.
4. بعد از ورود، منوها و دکمه‌ها بر اساس grantهای همان نقش نمایش داده می‌شوند.

اگر گذرواژه را بعد از ساخته‌شدن دیتابیس تغییر دهید، حساب موجود خودکار با گذرواژهٔ جدید بازنشانی نمی‌شود. برای یک اجرای نمایشی تازه، برنامه را متوقف و فایل محلی `pipe-mill.db` را فقط در محیط توسعه حذف کنید، سپس برنامه را با متغیر جدید اجرا کنید. حذف دیتابیس همهٔ داده‌های محلی را پاک می‌کند.

### کار با بخش‌های اصلی

- «اطلاعات پایه ← تجهیزات»: جستجو و مشاهدهٔ تجهیزات؛ مدیر می‌تواند ایجاد، ویرایش و حذف کند.
- «تولید ← سفارش‌های تولید»: سفارش، تجهیز، تعداد، تاریخ شروع و وضعیت را مدیریت می‌کند.
- «کنترل کیفیت ← بازرسی‌ها»: جزئیات بازرسی، نتیجه و توضیحات را نمایش می‌دهد. «تأیید بازرسی» و زبانهٔ «سوابق تأیید» مجوزهای مستقل دارند.
- «مدیریت ← مدیریت دسترسی‌ها»: فقط برای مدیر مجاز نمایش داده می‌شود. نقش را انتخاب و grant هر کلید فعال را روشن یا خاموش کنید.

### داده‌های تجاری Seedشده

- تجهیزات: `EQ-100` دستگاه شکل‌دهی و `EQ-210` کوره عملیات حرارتی
- سفارش تولید: `PO-1403-001` با تعداد برنامه‌ریزی‌شدهٔ ۱۲۰۰
- بازرسی: `QC-001` با نتیجهٔ «قابل قبول»

Seed داده‌ها idempotent است: اجرای دوباره نباید داده‌های نمونه یا مجوزها را تکراری کند. grantهای موجود هنگام همگام‌سازی catalog حفظ می‌شوند.

آزمون‌ها: `dotnet test PipeMillPermissions.slnx`. فایل SQLite در اجرای نخست ساخته و catalog در تراکنش همگام می‌شود. حذف کلید، آن را غیرفعال می‌کند و grant آن حفظ ولی غیرقابل استفاده می‌ماند.

## سناریوی پیشنهادی برای آزمایش در دو مرورگر

1. در پنجرهٔ عادی با `admin` و در پنجرهٔ ناشناس با `inspector` وارد شوید.
2. مدیر در «مدیریت دسترسی‌ها» نقش `Inspector` را انتخاب و `inspections.approve` را فعال کند.
3. Inspector وارد «بازرسی‌ها»، جزئیات `QC-001` شود و «تأیید بازرسی» را بزند.
4. مدیر همان grant را لغو کند، بدون اینکه Inspector از حساب خارج شود.
5. درخواست بعدی Inspector باید 403 بگیرد؛ snapshot رابط تازه و دکمهٔ تأیید حذف می‌شود.
6. جداگانه با `viewer` وارد شوید؛ فهرست‌ها قابل مشاهده‌اند ولی دکمه‌های حذف و ویرایش وجود ندارند و API مستقیم نیز عملیات را رد می‌کند.

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
