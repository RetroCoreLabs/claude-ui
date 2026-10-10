using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClaudeUsage.Services;

// Finds the OAuth access token that the Claude CLI login has stored on this
// machine. The token is read fresh for every request and is never written.
//
// Search order:
//   1. The file named by the CLAUDE_USAGE_CREDENTIALS environment variable.
//   2. macOS only: the login keychain item written by the Claude CLI.
//   3. .credentials.json in the Claude config folder, which is the folder
//      named by CLAUDE_CONFIG_DIR, or .claude in the home folder.
public static class CredentialStore
{
    public const string PathVariable = "CLAUDE_USAGE_CREDENTIALS";
    private const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";
    private const string FileName = ".credentials.json";

    // Keychain service name used by the Claude CLI on macOS.
    private const string KeychainService = "Claude Code-credentials";
    private const string SecurityTool = "security";
    private static readonly TimeSpan KeychainTimeout = TimeSpan.FromSeconds(10);

    public static string DefaultFilePath => Path.Combine(ConfigDir, FileName);

    private static string? ConfigDirOverride
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ConfigDirVariable);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    private static string ConfigDir => ConfigDirOverride
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    public static async Task<string> ReadAccessTokenAsync(CancellationToken cancellationToken)
    {
        var overridePath = Environment.GetEnvironmentVariable(PathVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return await ReadTokenFromFileAsync(overridePath, cancellationToken);
        }

        if (OperatingSystem.IsMacOS())
        {
            var keychainJson = await ReadKeychainAsync(cancellationToken);
            if (keychainJson is not null && TryGetToken(keychainJson, out var keychainToken))
            {
                return keychainToken;
            }
        }

        return await ReadTokenFromFileAsync(DefaultFilePath, cancellationToken);
    }

    private static async Task<string> ReadTokenFromFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new UsageException($"Credentials file not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        if (TryGetToken(json, out var token))
        {
            return token;
        }

        throw new UsageException($"No claudeAiOauth.accessToken in {path}");
    }

    private static bool TryGetToken(string json, out string token)
    {
        token = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("claudeAiOauth", out var oauth) &&
                oauth.ValueKind == JsonValueKind.Object &&
                oauth.TryGetProperty("accessToken", out var accessToken) &&
                accessToken.ValueKind == JsonValueKind.String &&
                accessToken.GetString() is { Length: > 0 } value)
            {
                token = value;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    // The service name gets a suffix when CLAUDE_CONFIG_DIR is set: a dash and
    // the first 8 hex digits of the SHA-256 of the config folder path.
    private static string KeychainServiceName()
    {
        if (ConfigDirOverride is not { } configDir)
        {
            return KeychainService;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(configDir.Normalize(NormalizationForm.FormC)));
        return $"{KeychainService}-{Convert.ToHexStringLower(hash)[..8]}";
    }

    // Returns the JSON text stored in the keychain item, or null if it could not be read.
    private static async Task<string?> ReadKeychainAsync(CancellationToken cancellationToken)
    {
        var account = Environment.GetEnvironmentVariable("USER");
        if (string.IsNullOrEmpty(account))
        {
            account = Environment.UserName;
        }

        var startInfo = new ProcessStartInfo(SecurityTool)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("find-generic-password");
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add(account);
        startInfo.ArgumentList.Add("-w");
        startInfo.ArgumentList.Add("-s");
        startInfo.ArgumentList.Add(KeychainServiceName());

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(KeychainTimeout);

            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException or IOException)
        {
            return null;
        }
    }
}
