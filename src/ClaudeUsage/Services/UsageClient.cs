using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeUsage.Models;

namespace ClaudeUsage.Services;

public class UsageException(string message) : Exception(message);

// HTTP 429. RetryAfter is the server's retry-after header, if it sent one.
public sealed class RateLimitedException(TimeSpan? retryAfter)
    : UsageException("HTTP 429: rate limited by the server.")
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public sealed class UsageClient
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    private const string BetaHeader = "oauth-2025-04-20";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<UsageResponse> FetchAsync(CancellationToken cancellationToken = default)
    {
        var token = await CredentialStore.ReadAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("anthropic-beta", BetaHeader);

        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            if (code == 429)
            {
                throw new RateLimitedException(response.Headers.RetryAfter?.Delta);
            }

            throw new UsageException(code == 401
                ? "HTTP 401: the login was rejected. Sign in again and press Refresh."
                : $"The usage request failed with HTTP {code}.");
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var usage = await JsonSerializer.DeserializeAsync<UsageResponse>(body, cancellationToken: cancellationToken);
        return usage ?? throw new UsageException("The usage response was empty.");
    }
}
