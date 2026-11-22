using Avalonia;
using TerrariaZoomPatcher.Core;

namespace TerrariaZoomPatcher.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Если переданы аргументы командной строки (как в Docker) — работаем в консоли
        if (args.Length > 0)
        {
            return RunCli(args);
        }

        // Иначе запускаем обычный графический интерфейс Avalonia
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static int RunCli(string[] args)
    {
        string inputPath = args[0];
        bool enableEightK = args.Contains("--8k", StringComparer.OrdinalIgnoreCase);
        bool restore = args.Contains("--restore", StringComparer.OrdinalIgnoreCase);

        // Если передана папка (например, /app/game из тома Docker), автоматически добавляем Terraria.exe
        string exePath = Directory.Exists(inputPath)
            ? Path.Combine(inputPath, "Terraria.exe")
            : inputPath;

        Console.WriteLine($"Целевой файл: {exePath}");

        FilePatchResult result = restore
            ? TerrariaFilePatcher.RestoreBackup(exePath)
            : TerrariaFilePatcher.Apply(exePath, enableEightK);

        Console.WriteLine(result.Message);
        return result.Success ? 0 : 1;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
