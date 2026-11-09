using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PipeMillPermissions.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<SessionService>();
await builder.Build().RunAsync();
