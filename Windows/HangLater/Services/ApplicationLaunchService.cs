using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace HangLater.Windows.Services;

/// <summary>Window-owner executables and user-facing launchers need not be the same file.</summary>
public static class ApplicationLaunchService
{
    public sealed record ProductIdentity(string? Company, string? Product);

    public static string? Resolve(string? windowExecutable, string? capturedLauncher = null,
        Func<string, ProductIdentity?>? inspect = null)
    {
        if (string.IsNullOrWhiteSpace(windowExecutable) || !Path.IsPathFullyQualified(windowExecutable)) return null;
        inspect ??= Inspect;
        try
        {
            string executable = Path.GetFullPath(windowExecutable);
            ProductIdentity? identity = File.Exists(executable) ? inspect(executable) : null;
            if (!string.IsNullOrWhiteSpace(capturedLauncher) && Path.IsPathFullyQualified(capturedLauncher) &&
                File.Exists(capturedLauncher) && (SamePath(executable,capturedLauncher) || SameProduct(identity,inspect(capturedLauncher))))
                return Path.GetFullPath(capturedLauncher);

            string? registered = RegisteredPath(Path.GetFileName(executable));
            if (registered != null && (!File.Exists(executable) || SamePath(executable,registered) || SameProduct(identity,inspect(registered)))) return registered;

            // Common installer layout: App/App.exe bootstraps App/bin/App.exe.
            // Never guess an arbitrary parent binary: require bin + same filename + matching product/publisher.
            string? directory = Path.GetDirectoryName(executable);
            if (directory != null && Path.GetFileName(directory).Equals("bin",StringComparison.OrdinalIgnoreCase))
            {
                string? parent = Path.GetDirectoryName(directory);
                if (parent != null)
                {
                    string launcher = Path.Combine(parent,Path.GetFileName(executable));
                    if (File.Exists(launcher) && SameProduct(identity,inspect(launcher))) return launcher;
                }
            }
            return File.Exists(executable) ? executable : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return File.Exists(windowExecutable) ? windowExecutable : null;
        }
    }

    public static ProcessStartInfo? StartInfo(HangingTaskTarget target)
    {
        string? executable = Resolve(target.ExecutablePath,target.LaunchExecutablePath);
        return executable == null ? null : new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? ""
        };
    }

    public static bool SameProduct(ProductIdentity? a, ProductIdentity? b) => a != null && b != null &&
        !string.IsNullOrWhiteSpace(a.Company) && !string.IsNullOrWhiteSpace(a.Product) &&
        string.Equals(a.Company.Trim(),b.Company?.Trim(),StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Product.Trim(),b.Product?.Trim(),StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a,string b) => string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    private static ProductIdentity? Inspect(string file)
    {
        try { var version=FileVersionInfo.GetVersionInfo(file); return new(version.CompanyName,version.ProductName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private static string? RegisteredPath(string name)
    {
        foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using RegistryKey? key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+name,false);
            if (key?.GetValue(null) is not string value) continue;
            string path = Environment.ExpandEnvironmentVariables(value).Trim().Trim('"');
            if (Path.IsPathFullyQualified(path) && File.Exists(path)) return Path.GetFullPath(path);
        }
        return null;
    }
}
