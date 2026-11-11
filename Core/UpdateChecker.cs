using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace KeyFlip.Core;

public sealed record UpdateCheckResult(
    bool Succeeded,
    Version CurrentVersion,
    Version? LatestVersion,
    string? LatestTag,
    string ReleaseUrl,
    bool IsUpdateAvailable,
    string? ErrorMessage = null);

public sealed class UpdateChecker : IDisposable
{
    public const string ReleasesPageUrl = "https://github.com/MHKarami97/KeyFlip/releases";
    public const string LatestReleaseUrl = "https://github.com/MHKarami97/KeyFlip/releases/latest";
    public const string LatestApiUrl = "https://api.github.com/repos/MHKarami97/KeyFlip/releases/latest";
    private const string TagPathPrefix = "/MHKarami97/KeyFlip/releases/tag/";
    private const string ReleasesPathPrefix = "/MHKarami97/KeyFlip/releases";

    public static readonly int[] SupportedIntervalHours = [1, 3, 6, 12, 24];

    private static readonly Uri LatestReleaseUri = new(LatestReleaseUrl);
    private readonly HttpClient _http;

    public Version CurrentVersion { get; }

    public UpdateChecker(HttpMessageHandler? handler = null, Version? currentVersion = null)
    {
        CurrentVersion = currentVersion is not null ? NormalizeVersion(currentVersion) : ReadAssemblyVersion();

        if (handler is null)
        {
            var defaultHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _http = new HttpClient(defaultHandler, disposeHandler: true);
        }
        else
        {
            _http = new HttpClient(handler, disposeHandler: false);
        }

        _http.Timeout = TimeSpan.FromSeconds(15);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KeyFlip", FormatVersion(CurrentVersion)));
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, LatestReleaseUri);
            using var headResponse = await _http.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)headResponse.StatusCode is >= 300 and < 400 && headResponse.Headers.Location is { } location)
            {
                if (TryResolveRedirect(location, out var noReleasesYet, out var tag, out var releaseUrl))
                {
                    if (noReleasesYet)
                    {
                        return new UpdateCheckResult(true, CurrentVersion, null, null, ReleasesPageUrl, false);
                    }

                    if (TryParseVersion(tag, out var latest, out var isPrerelease) && !isPrerelease)
                    {
                        return new UpdateCheckResult(
                            true,
                            CurrentVersion,
                            latest,
                            tag,
                            releaseUrl,
                            latest > CurrentVersion);
                    }
                }
            }

            return await CheckViaApiAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return new UpdateCheckResult(false, CurrentVersion, null, null, ReleasesPageUrl, false, ex.Message);
        }
    }

    public void Dispose() => _http.Dispose();

    public static Version ReadAssemblyVersion()
    {
        var assembly = typeof(UpdateChecker).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (TryParseVersion(informational, out var infoVersion, out _))
        {
            return infoVersion;
        }

        var assemblyVersion = assembly.GetName().Version;
        return assemblyVersion is not null ? NormalizeVersion(assemblyVersion) : new Version(1, 0, 0, 0);
    }

    public static string FormatVersion(Version version)
    {
        var normalized = NormalizeVersion(version);
        return normalized.Revision > 0
            ? $"{normalized.Major}.{normalized.Minor}.{normalized.Build}.{normalized.Revision}"
            : $"{normalized.Major}.{normalized.Minor}.{normalized.Build}";
    }

    public static Version NormalizeVersion(Version version) =>
        new(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));

    public static bool TryParseVersion(string? raw, out Version version, out bool isPrerelease)
    {
        version = new Version(0, 0, 0, 0);
        isPrerelease = false;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (text[0] is 'v' or 'V')
        {
            text = text[1..];
        }

        var plusIndex = text.IndexOf('+');
        if (plusIndex >= 0)
        {
            text = text[..plusIndex];
        }

        var dashIndex = text.IndexOf('-');
        if (dashIndex >= 0)
        {
            isPrerelease = dashIndex + 1 < text.Length;
            text = text[..dashIndex];
        }

        var parts = text.Split('.');
        if (parts.Length < 2 || parts.Length > 4)
        {
            return false;
        }

        Span<int> numbers = stackalloc int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || !int.TryParse(parts[i], out var value) || value < 0)
            {
                return false;
            }

            numbers[i] = value;
        }

        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }

    public static bool IsTrustedReleaseUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith(ReleasesPathPrefix, StringComparison.OrdinalIgnoreCase);

    public static void OpenReleasePage(string? url)
    {
        var target = IsTrustedReleaseUrl(url) ? url! : ReleasesPageUrl;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    public static int NormalizeIntervalHours(int hours) => Math.Clamp(hours <= 0 ? 6 : hours, 1, 168);

    private async Task<UpdateCheckResult> CheckViaApiAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new UpdateCheckResult(true, CurrentVersion, null, null, ReleasesPageUrl, false);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new UpdateCheckResult(false, CurrentVersion, null, null, ReleasesPageUrl, false, $"HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var info = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.GitHubReleaseInfo, ct);
        if (info is null || info.Draft || info.Prerelease || string.IsNullOrWhiteSpace(info.TagName))
        {
            return new UpdateCheckResult(true, CurrentVersion, null, null, ReleasesPageUrl, false);
        }

        var tag = info.TagName.Trim();
        if (!TryParseVersion(tag, out var latest, out var isPrerelease) || isPrerelease)
        {
            return new UpdateCheckResult(false, CurrentVersion, null, tag, ReleasesPageUrl, false, "Invalid release tag");
        }

        var url = IsTrustedReleaseUrl(info.HtmlUrl)
            ? info.HtmlUrl!
            : $"{ReleasesPageUrl}/tag/{Uri.EscapeDataString(tag)}";

        return new UpdateCheckResult(true, CurrentVersion, latest, tag, url, latest > CurrentVersion);
    }

    private static bool TryResolveRedirect(Uri location, out bool noReleasesYet, out string tag, out string releaseUrl)
    {
        noReleasesYet = false;
        tag = string.Empty;
        releaseUrl = ReleasesPageUrl;

        var resolved = location.IsAbsoluteUri ? location : new Uri(LatestReleaseUri, location);
        if (resolved.Scheme != Uri.UriSchemeHttps || !string.Equals(resolved.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = resolved.AbsolutePath;
        if (string.Equals(path.TrimEnd('/'), ReleasesPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            noReleasesYet = true;
            return true;
        }

        if (!path.StartsWith(TagPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rawTag = path[TagPathPrefix.Length..].TrimEnd('/');
        var slashIndex = rawTag.IndexOf('/');
        if (slashIndex >= 0)
        {
            rawTag = rawTag[..slashIndex];
        }

        tag = Uri.UnescapeDataString(rawTag).Trim();
        if (tag.Length == 0)
        {
            return false;
        }

        releaseUrl = $"{ReleasesPageUrl}/tag/{Uri.EscapeDataString(tag)}";
        return true;
    }
}
