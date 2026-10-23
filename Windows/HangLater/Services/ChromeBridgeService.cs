using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace HangLater.Windows.Services;

public static class ChromeBridgeService
{
	// Extract the optional extension from the portable EXE, not a sidecar folder.
	public static string EnsureExtensionFiles(string root)
	{
		const string prefix = "HangLater.BrowserExtension.";
		string folder = Path.Combine(root, "BrowserBridge", "ChromeExtension");
		Directory.CreateDirectory(folder);
		var assembly = typeof(ChromeBridgeService).Assembly;
		foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
		{
			string fileName = name.Substring(prefix.Length);
			using Stream input = assembly.GetManifestResourceStream(name)!;
			using var memory = new MemoryStream();
			input.CopyTo(memory);
			byte[] bytes = memory.ToArray();
			string path = Path.Combine(folder, fileName);
			if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
		}
		if (!File.Exists(Path.Combine(folder, "manifest.json"))) throw new InvalidDataException("The bundled browser extension is missing.");
		return folder;
	}

	public const string HostName = "app.hanglater.capture";

	public const string ExtensionId = "ahkgcekpbdbdkgmkfefnlpacglodpiji";

	private const int MaxMessageBytes = 16777216;

	public static string Install(string root, string executablePath)
	{
		string text = Path.Combine(root, "BrowserBridge");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, "app.hanglater.capture.json");
		var value = new
		{
			name = "app.hanglater.capture",
			description = "Hang Later browser capture bridge",
			path = executablePath,
			type = "stdio",
			allowed_origins = new string[1] { "chrome-extension://ahkgcekpbdbdkgmkfefnlpacglodpiji/" }
		};
		File.WriteAllText(text2, JsonSerializer.Serialize(value, JsonSupport.Options));
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Google\\Chrome\\NativeMessagingHosts\\app.hanglater.capture");
		registryKey.SetValue(null, text2);
		using RegistryKey registryKey2 = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Edge\\NativeMessagingHosts\\app.hanglater.capture");
		registryKey2.SetValue(null, text2);
		return text2;
	}

	public static async Task<int> RunHostAsync(string root, string origin)
	{
		object value3;
		try
		{
			if (origin != "chrome-extension://ahkgcekpbdbdkgmkfefnlpacglodpiji/")
			{
				throw new InvalidOperationException("Unauthorized extension");
			}
			using Stream input = Console.OpenStandardInput();
			byte[] lengthBuffer = new byte[4];
			await ReadExactly(input, lengthBuffer);
			int num = BitConverter.ToInt32(lengthBuffer, 0);
			if (num <= 0 || num > 16777216)
			{
				throw new InvalidDataException("Capture is too large.");
			}
			byte[] payload = new byte[num];
			await ReadExactly(input, payload);
			using JsonDocument message = JsonDocument.Parse(payload);
			string text = message.RootElement.GetProperty("url").GetString() ?? "";
			if (!TaskRepository.IsWebUrl(text))
			{
				throw new InvalidDataException("Only http and https pages can be saved.");
			}
			JsonElement value;
			string text2 = (message.RootElement.TryGetProperty("title", out value) ? (value.GetString() ?? new Uri(text).Host) : new Uri(text).Host);
			Dictionary<string, string> dictionary = new Dictionary<string, string>
			{
				["url"] = text,
				["title"] = text2.Substring(0, Math.Min(text2.Length, 1000))
			};
			if (message.RootElement.TryGetProperty("screenshot", out var value2) && value2.ValueKind == JsonValueKind.String)
			{
				string text3 = value2.GetString();
				byte[] array = Convert.FromBase64String(text3);
				if (array.Length < 8 || !array.Take(8).SequenceEqual(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 }))
				{
					throw new InvalidDataException("Expected a PNG screenshot.");
				}
				dictionary["screenshot"] = text3;
			}
			string text4 = Path.Combine(root, "BrowserBridge", "Inbox");
			Directory.CreateDirectory(text4);
			string final = Path.Combine(text4, $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}.json");
			string temp = final + ".tmp";
			await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(dictionary));
			File.Move(temp, final);
			value3 = new
			{
				ok = true
			};
		}
		catch (Exception ex)
		{
			value3 = new
			{
				ok = false,
				error = ex.Message
			};
		}
		byte[] outputPayload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value3));
		using Stream output = Console.OpenStandardOutput();
		await output.WriteAsync(BitConverter.GetBytes(outputPayload.Length));
		await output.WriteAsync(outputPayload);
		await output.FlushAsync();
		return 0;
	}

	private static async Task ReadExactly(Stream stream, byte[] buffer)
	{
		int num;
		for (int offset = 0; offset < buffer.Length; offset += num)
		{
			num = await stream.ReadAsync(buffer.AsMemory(offset));
			if (num == 0)
			{
				throw new EndOfStreamException("Incomplete native message");
			}
		}
	}
}
