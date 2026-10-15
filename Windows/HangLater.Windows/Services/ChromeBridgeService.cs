using System.Text;
using System.Text.Json;
using System.IO;
using Microsoft.Win32;

namespace HangLater.Windows.Services;

public static class ChromeBridgeService
{
    public const string HostName = "app.hanglater.capture";
    public const string ExtensionId = "ahkgcekpbdbdkgmkfefnlpacglodpiji";
    private const int MaxMessageBytes = 16 * 1024 * 1024;

    public static string Install(string root, string executablePath)
    {
        var directory = Path.Combine(root, "BrowserBridge"); Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "app.hanglater.capture.json");
        var manifest = new
        {
            name = HostName,
            description = "Hang Later browser capture bridge",
            path = executablePath,
            type = "stdio",
            allowed_origins = new[] { $"chrome-extension://{ExtensionId}/" }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonSupport.Options));
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Google\Chrome\NativeMessagingHosts\{HostName}");
        key.SetValue(null, manifestPath);
        using var edge = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Edge\NativeMessagingHosts\{HostName}");
        edge.SetValue(null, manifestPath);
        return manifestPath;
    }

    public static async Task<int> RunHostAsync(string root, string origin)
    {
        object response;
        try
        {
            if (origin != $"chrome-extension://{ExtensionId}/") throw new InvalidOperationException("Unauthorized extension");
            using var input = Console.OpenStandardInput();
            var lengthBuffer = new byte[4]; await ReadExactly(input, lengthBuffer);
            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > MaxMessageBytes) throw new InvalidDataException("Capture is too large.");
            var payload = new byte[length]; await ReadExactly(input, payload);
            using var message = JsonDocument.Parse(payload);
            var url = message.RootElement.GetProperty("url").GetString() ?? "";
            if (!TaskRepository.IsWebUrl(url)) throw new InvalidDataException("Only http and https pages can be saved.");
            var title = message.RootElement.TryGetProperty("title", out var titleValue) ? titleValue.GetString() ?? new Uri(url).Host : new Uri(url).Host;
            var capture = new Dictionary<string, string> { ["url"] = url, ["title"] = title[..Math.Min(title.Length, 1000)] };
            if (message.RootElement.TryGetProperty("screenshot", out var screenshot) && screenshot.ValueKind == JsonValueKind.String)
            {
                var encoded = screenshot.GetString()!; var bytes = Convert.FromBase64String(encoded);
                if (bytes.Length < 8 || !bytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("Expected a PNG screenshot.");
                capture["screenshot"] = encoded;
            }
            var inbox = Path.Combine(root, "BrowserBridge", "Inbox"); Directory.CreateDirectory(inbox);
            var final = Path.Combine(inbox, $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}.json");
            var temp = final + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(capture)); File.Move(temp, final);
            response = new { ok = true };
        }
        catch (Exception ex) { response = new { ok = false, error = ex.Message }; }
        var outputPayload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
        using var output = Console.OpenStandardOutput(); await output.WriteAsync(BitConverter.GetBytes(outputPayload.Length)); await output.WriteAsync(outputPayload); await output.FlushAsync();
        return 0;
    }
    private static async Task ReadExactly(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(offset));
            if (count == 0) throw new EndOfStreamException("Incomplete native message"); offset += count;
        }
    }
}
