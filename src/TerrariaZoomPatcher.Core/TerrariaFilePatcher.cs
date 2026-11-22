namespace TerrariaZoomPatcher.Core;

public sealed record FilePatchResult(bool Success, string Message, string? BackupPath = null);

public static class TerrariaFilePatcher
{
    public static FilePatchResult Apply(string exePath, bool enableEightK)
    {
        if (!TryGetTerrariaPath(exePath, out var fullPath, out var validationError))
        {
            return new FilePatchResult(false, validationError);
        }

        try
        {
            var originalBytes = File.ReadAllBytes(fullPath);
            var plan = TerrariaPatchEngine.CreatePlan(originalBytes, enableEightK);
            if (!plan.Success || plan.PatchedBytes is null)
            {
                return new FilePatchResult(false, plan.Message);
            }

            if (!plan.HasChanges)
            {
                return new FilePatchResult(true, plan.Message, GetBackupPath(fullPath));
            }

            var backupPath = GetBackupPath(fullPath);
            if (!File.Exists(backupPath))
            {
                WriteNewFile(backupPath, originalBytes);
            }

            var temporaryPath = GetTemporaryPath(fullPath);
            try
            {
                WriteNewFile(temporaryPath, plan.PatchedBytes);
                File.Move(temporaryPath, fullPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            var backupNote = File.Exists(backupPath)
                ? $" Резервная копия: {backupPath}"
                : string.Empty;
            return new FilePatchResult(
                true,
                plan.Message + backupNote,
                backupPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new FilePatchResult(
                false,
                $"Не удалось изменить файл. Проверьте, что Terraria закрыта и файл доступен для записи. {exception.Message}");
        }
    }

    public static FilePatchResult RestoreBackup(string exePath)
    {
        if (!TryGetTerrariaPath(exePath, out var fullPath, out var validationError))
        {
            return new FilePatchResult(false, validationError);
        }

        var backupPath = GetBackupPath(fullPath);
        if (!File.Exists(backupPath))
        {
            return new FilePatchResult(false, $"Резервная копия не найдена: {backupPath}");
        }

        try
        {
            var temporaryPath = GetTemporaryPath(fullPath);
            try
            {
                File.Copy(backupPath, temporaryPath, false);
                File.Move(temporaryPath, fullPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            return new FilePatchResult(
                true,
                $"Исходный файл восстановлен из копии: {backupPath}",
                backupPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new FilePatchResult(
                false,
                $"Не удалось восстановить файл. Закройте Terraria и проверьте доступ к папке. {exception.Message}");
        }
    }

    public static string GetBackupPath(string exePath) => exePath + ".bak";

    private static bool TryGetTerrariaPath(
        string exePath,
        out string fullPath,
        out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(exePath))
        {
            error = "Сначала выберите файл Terraria.exe.";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(exePath.Trim().Trim('"'));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            error = $"Неверный путь к файлу: {exception.Message}";
            return false;
        }

        if (!string.Equals(
                Path.GetFileName(fullPath),
                "Terraria.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            error = "Выберите файл с именем Terraria.exe.";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            error = $"Файл не найден: {fullPath}";
            return false;
        }

        return true;
    }

    private static string GetTemporaryPath(string exePath)
    {
        var directory = Path.GetDirectoryName(exePath)
            ?? throw new IOException("Не удалось определить папку Terraria.");
        return Path.Combine(
            directory,
            $".Terraria.{Guid.NewGuid():N}.patch.tmp");
    }

    private static void WriteNewFile(string path, byte[] bytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }
}
