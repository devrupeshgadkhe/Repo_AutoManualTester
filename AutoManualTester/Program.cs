using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AutoManualTester;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--apply-update")
        {
            ApplyUpdate(args);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void ApplyUpdate(string[] args)
    {
        try
        {
            var zipPath = args[1];
            var parentPid = int.Parse(args[2]);
            var expectedHash = args[3];

            try { Process.GetProcessById(parentPid).WaitForExit(); }
            catch (ArgumentException) { }

            using var stream = File.OpenRead(zipPath);
            var actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(actualHash),
                    System.Text.Encoding.ASCII.GetBytes(expectedHash.ToLowerInvariant())))
                throw new InvalidDataException("Update package checksum validation failed.");

            ZipFile.ExtractToDirectory(zipPath, AppContext.BaseDirectory, overwriteFiles: true);
            File.Delete(zipPath);
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(Path.GetTempPath(), "AutoManualTester-update-error.txt");
            File.WriteAllText(logPath, ex.ToString());
        }
    }
}