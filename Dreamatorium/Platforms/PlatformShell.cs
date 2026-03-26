using System.Diagnostics;

namespace Dreamatorium.Platforms;

public static class PlatformShell
{
    public static void RevealInFileManager(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        if (TryRun("open", "-R", fullPath))
        {
            return;
        }

        OpenDirectoryFallback("open", fullPath);
    }

    private static void OpenDirectoryFallback(string opener, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        if (!TryRun(opener, directory))
        {
            Console.WriteLine($"Failed to open file manager for capture path: {filePath}");
        }
    }

    private static bool TryRun(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(startInfo);

            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
