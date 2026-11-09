using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PipeMillPermissions.Client.Shared;
using PipeMillPermissions.Data;
using PipeMillPermissions.Permissions;

namespace PipeMillPermissions.Tests;

public sealed class PermissionTests
{
    [Fact]
    public void Discovery_finds_all_modules_and_derived_operations_before_navigation()
    {
        var catalog = FeatureCatalog.Discover(typeof(IFeatureModule).Assembly);
        Assert.Equal(3, catalog.Modules.Count);
        Assert.Contains(catalog.Capabilities, x => x.Key == "inspections.approve");
        Assert.Contains(catalog.Capabilities, x => x.Key == "inspections.approval-history.read");
        Assert.Equal(5, catalog.Capabilities.Count(x => x.Key.StartsWith("equipment.")));
    }

    [Fact]
    public async Task Catalog_sync_is_idempotent_and_preserves_grants_when_label_changes()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options); await db.Database.EnsureCreatedAsync();
        var catalog = FeatureCatalog.Discover(typeof(IFeatureModule).Assembly);
        var sync = new PermissionCatalogSynchronizer(db, catalog); await sync.SyncAsync(); var first = await db.PermissionCatalog.CountAsync(); await sync.SyncAsync();
        Assert.Equal(first, await db.PermissionCatalog.CountAsync());
        Assert.Equal(catalog.Capabilities.Count, first);
    }

    [Fact]
    public async Task Anonymous_api_is_401_problem_details_without_redirect()
    {
        await using var factory = new TestFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/equipment?page=1&pageSize=10");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("authentication_required", await response.Content.ReadAsStringAsync());
    }
}

public sealed class TestFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"pipe-mill-tests-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>)); services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
        });
    }
    public new async ValueTask DisposeAsync() { await base.DisposeAsync(); if (File.Exists(dbPath)) File.Delete(dbPath); }
}
