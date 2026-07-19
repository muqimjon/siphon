using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Telegram.Bot;

namespace Siphon.Bot.Backend;

public sealed class BackendException(string code, string? message) : Exception(message ?? code)
{
    public string Code { get; } = code;
}

public sealed class SiphonApi(HttpClient http, IHttpClientFactory factory)
{
    public static string TelegramFileBase { get; set; } = "";

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ProbeResult> ProbeAsync(string url, long? telegramUserId, CancellationToken ct) =>
        PostAsync<ProbeResult>("api/v1/probe", new { url }, telegramUserId, ct);

    public async Task<string> CreateJobAsync(string url, string output, string format, string? formatId, long? telegramUserId, CancellationToken ct) =>
        (await PostAsync<JobCreated>("api/v1/jobs", new { url, output, format, formatId }, telegramUserId, ct)).JobId;

    public async Task<string?> TelegramFileUrlAsync(Telegram.Bot.ITelegramBotClient bot, string fileId, CancellationToken ct)
    {
        try
        {
            var file = await bot.GetFile(fileId, ct);
            return file.FilePath is null ? null : $"{TelegramFileBase}/{file.FilePath}";
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task<ConnectCode> IssueConnectCodeAsync(long telegramUserId, string? username, string? firstName, CancellationToken ct) =>
        PostAsync<ConnectCode>("api/v1/telegram/code", new { telegramUserId, username, firstName }, null, ct);

    public Task<LinkResult> RedeemLinkAsync(string token, long telegramUserId, string? username, string? firstName, CancellationToken ct) =>
        PostAsync<LinkResult>("api/v1/telegram/redeem", new { token, telegramUserId, username, firstName }, null, ct);

    public async Task<JobStatus> GetJobAsync(string id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"api/v1/jobs/{id}", ct);
        return await ReadAsync<JobStatus>(response, ct);
    }

    public async Task<Stream> OpenFileAsync(string fileUrl, CancellationToken ct)
    {
        var response = await factory.CreateClient("siphon-files")
            .GetAsync(fileUrl.TrimStart('/'), HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct);
    }

    async Task<T> PostAsync<T>(string path, object body, long? telegramUserId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: Json)
        };
        if (telegramUserId is long id) request.Headers.Add("X-Telegram-User", id.ToString());
        using var response = await http.SendAsync(request, ct);
        return await ReadAsync<T>(response, ct);
    }

    static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var code = response.StatusCode == HttpStatusCode.TooManyRequests ? "rate-limited" : "unknown";
            string? message = null;
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
                if (problem.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                    code = c.GetString()!;
                if (problem.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                    message = d.GetString();
            }
            catch
            {
            }
            throw new BackendException(code, message);
        }
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }
}
