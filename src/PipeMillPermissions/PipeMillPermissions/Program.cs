using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PipeMillPermissions.Client.Pages;
using PipeMillPermissions.Client.Shared;
using PipeMillPermissions.Components;
using PipeMillPermissions.Data;
using PipeMillPermissions.Infrastructure;
using PipeMillPermissions.Permissions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=pipe-mill.db"));
builder.Services.AddIdentityCore<AppUser>(o => { o.Password.RequiredLength = 10; o.SignIn.RequireConfirmedAccount = false; })
    .AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o => { o.Cookie.Name = "PipeMill.Auth"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.SlidingExpiration = true; o.Events = new CookieAuthenticationEvents { OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; }, OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; } }; });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
var catalog = FeatureCatalog.Discover(typeof(IFeatureModule).Assembly);
builder.Services.AddSingleton(catalog);
builder.Services.AddScoped<PermissionCatalogSynchronizer>();
builder.Services.AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN");

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.UseWebAssemblyDebugging(); else { app.UseExceptionHandler("/Error", createScopeForErrors: true); app.UseHsts(); }
app.UseHttpsRedirection(); app.UseAuthentication(); app.UseAuthorization(); app.UseStatusCodePages(c => ApiStatusHandler.HandleAsync(c.HttpContext));
app.UseAntiforgery(); app.MapStaticAssets(); app.MapApplicationApis();
app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(typeof(PipeMillPermissions.Client._Imports).Assembly);
await AppSeeder.SeedAsync(app.Services, app.Configuration, app.Environment);
app.Run();

public partial class Program { }
