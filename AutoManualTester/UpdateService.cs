using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace AutoManualTester;

internal sealed class UpdateService : IDisposable
{
    private const string ManifestUrl = "https://github.com/devrupeshgadkhe/Repo_AutoManualTester/releases/latest/download/version.json";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly System.Threading.Timer _timer;
    private int _checking;
    private bool _updateStarted;
    private bool _disposed;

    public event Action<string>? StatusChanged;

    public UpdateService()
    {
        _timer = new System.Threading.Timer(async _ => await CheckAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }

    private async Task CheckAsync()
    {
        if (_disposed || _updateStarted || Interlocked.Exchange(ref _checking, 1) != 0) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ManifestUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) ||
                string.IsNullOrWhiteSpace(manifest.ZipUrl) || string.IsNullOrWhiteSpace(manifest.Sha256)) return;

            if (!Version.TryParse(manifest.Version, out var latest)) return;
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
            if (latest <= current) return;

            _updateStarted = true;
            StatusChanged?.Invoke($"Update {latest} found. Downloading automatically...");
            await DownloadAndApplyAsync(manifest);
        }
        catch
        {
            // Retry automatically on the next interval when network access is unavailable.
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    private async Task DownloadAndApplyAsync(UpdateManifest manifest)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "AutoManualTesterUpdate");
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, "update.zip");

        using (var response = await Http.GetAsync(manifest.ZipUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(zipPath);
            await input.CopyToAsync(output);
        }

        await using var file = File.OpenRead(zipPath);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(actualHash),
                System.Text.Encoding.ASCII.GetBytes(manifest.Sha256.Trim().ToLowerInvariant())))
            throw new InvalidDataException("Update package checksum validation failed.");

        var currentExe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate application executable.");
        var helperExe = Path.Combine(tempRoot, "AutoManualTesterUpdater.exe");
        File.Copy(currentExe, helperExe, overwrite: true);

        var start = new ProcessStartInfo(helperExe) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--apply-update");
        start.ArgumentList.Add(zipPath);
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add(actualHash);
        Process.Start(start);

        StatusChanged?.Invoke($"Update {manifest.Version} verified. Restarting...");
        Environment.Exit(0);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }

    private sealed class UpdateManifest
    {
        public string Version { get; set; } = "";
        public string ZipUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }
}