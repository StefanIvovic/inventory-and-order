using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Identity.Tests;

internal static class ApiClient
{
    public static Task<HttpResponseMessage> GetAsync(this HttpClient client, string path, string? token) =>
        client.SendAsync(HttpMethod.Get, path, token);

    public static Task<HttpResponseMessage> PostAsync(this HttpClient client, string path, object? body, string? token) =>
        client.SendAsync(HttpMethod.Post, path, token, body);

    public static Task<HttpResponseMessage> PutAsync(this HttpClient client, string path, string? token) =>
        client.SendAsync(HttpMethod.Put, path, token);

    public static Task<HttpResponseMessage> DeleteAsync(this HttpClient client, string path, string? token) =>
        client.SendAsync(HttpMethod.Delete, path, token);

    private static async Task<HttpResponseMessage> SendAsync(this HttpClient client, HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
