using System.Net.Http.Json;
using PipeMillPermissions.Client.Shared;

namespace PipeMillPermissions.Client.Services;

public sealed class SessionService(HttpClient http)
{
    public SessionDto Current { get; private set; } = new(false, null, [], null);
    public event Action? Changed;
    public async Task RefreshAsync() { Current = await http.GetFromJsonAsync<SessionDto>("api/auth/session") ?? Current; Changed?.Invoke(); }
    public bool Can(string permission) => Current.Permissions.Contains(permission, StringComparer.Ordinal);
    // PERMISSION-UI
    public bool CanAccess(string permission) => Can(permission);
    public async Task<HttpResponseMessage> SendMutationAsync(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (Current.AntiforgeryToken is not null) request.Headers.Add("X-XSRF-TOKEN", Current.AntiforgeryToken);
        var response = await http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) await RefreshAsync();
        return response;
    }
}
