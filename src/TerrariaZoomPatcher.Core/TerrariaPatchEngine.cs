namespace TerrariaZoomPatcher.Core;

public sealed record PatchPlan(
    bool Success,
    bool HasChanges,
    bool WidescreenEnabled,
    bool EightKLimitsEnabled,
    string Message,
    byte[]? PatchedBytes);

public static class TerrariaPatchEngine
{
    private static readonly byte[] SupportWideScreenAnchor =
        [0x20, 0x00, 0x08, 0x00, 0x00, 0x80];

    private static readonly byte[] FourKStaticStore =
        [0x20, 0x00, 0x10, 0x00, 0x00, 0x80];

    private static readonly byte[] EightKStaticStore =
        [0x20, 0x00, 0x20, 0x00, 0x00, 0x80];

    private static readonly byte[] FourKInteger = [0x20, 0x00, 0x10, 0x00, 0x00];
    private static readonly byte[] EightKInteger = [0x20, 0x00, 0x20, 0x00, 0x00];
    private static readonly byte[] LocalStoreOpcodes = [0x0A, 0x0B, 0x0C, 0x0D, 0x13];
    private static readonly byte[] HidefBranchOpcodes = [0x33, 0x40, 0xFE];

    public static PatchPlan CreatePlan(byte[] originalBytes, bool enableEightK)
    {
        ArgumentNullException.ThrowIfNull(originalBytes);
        var patchedBytes = originalBytes.ToArray();

        if (!TryPatchSupportWideScreen(
                patchedBytes,
                out var widescreenChanged,
                out var widescreenError))
        {
            return Failed(widescreenError);
        }

        var eightKChanged = false;
        if (enableEightK &&
            !TryPatchEightKLimits(patchedBytes, out eightKChanged, out var eightKError))
        {
            return Failed(eightKError);
        }

        var changed = widescreenChanged || eightKChanged;
        var message = changed
            ? enableEightK
                ? "Поддержка широкого экрана и лимиты 8K готовы к применению."
                : "Поддержка широкого экрана готова к применению."
            : enableEightK
                ? "Файл уже содержит патч широкого экрана и лимиты 8K."
                : "Патч широкого экрана уже установлен.";

        return new PatchPlan(
            true,
            changed,
            true,
            enableEightK,
            message,
            patchedBytes);
    }

    private static bool TryPatchSupportWideScreen(
        byte[] data,
        out bool changed,
        out string error)
    {
        changed = false;
        error = string.Empty;
        var unpatchedOffsets = new List<int>();
        var patchedOffsets = new List<int>();

        for (var position = 0; position <= data.Length - 16; position++)
        {
            if (!Matches(data, position, SupportWideScreenAnchor) ||
                data[position + 9] != 0x04)
            {
                continue;
            }

            var target = position + 10;
            if (data[target + 1] != 0x80 || data[target + 5] != 0x04)
            {
                continue;
            }

            if (data[target] == 0x16)
            {
                unpatchedOffsets.Add(target);
            }
            else if (data[target] == 0x17)
            {
                patchedOffsets.Add(target);
            }
        }

        if (unpatchedOffsets.Count + patchedOffsets.Count != 1)
        {
            error = unpatchedOffsets.Count + patchedOffsets.Count == 0
                ? "Не найден безопасно распознаваемый флаг SupportWideScreen. Этот файл не изменён."
                : "Найдено несколько возможных флагов SupportWideScreen. Этот файл не изменён.";
            return false;
        }

        if (unpatchedOffsets.Count == 1)
        {
            data[unpatchedOffsets[0]] = 0x17;
            changed = true;
        }

        return true;
    }

    private static bool TryPatchEightKLimits(
        byte[] data,
        out bool changed,
        out string error)
    {
        changed = false;
        error = string.Empty;

        var fourKGroups = FindStaticStoreGroups(data, FourKStaticStore);
        var eightKGroups = FindStaticStoreGroups(data, EightKStaticStore);
        if (fourKGroups.Count + eightKGroups.Count != 1)
        {
            error = fourKGroups.Count + eightKGroups.Count == 0
                ? "Не найдены все необходимые лимиты 8K. Игра не изменена."
                : "Найдено несколько возможных наборов лимитов 8K. Игра не изменена.";
            return false;
        }

        if (fourKGroups.Count == 1)
        {
            var groupStart = fourKGroups[0];
            for (var index = 0; index < 3; index++)
            {
                data[groupStart + index * 10 + 2] = 0x20;
            }

            changed = true;
        }

        var fourKTargets = FindInitTargetsValues(data, FourKInteger);
        var eightKTargets = FindInitTargetsValues(data, EightKInteger);
        if (fourKTargets.Count + eightKTargets.Count != 1)
        {
            error = fourKTargets.Count + eightKTargets.Count == 0
                ? "Не найден лимит рендеринга для 8K. Игра не изменена."
                : "Найдено несколько возможных лимитов рендеринга для 8K. Игра не изменена.";
            return false;
        }

        if (fourKTargets.Count == 1)
        {
            data[fourKTargets[0] + 2] = 0x20;
            changed = true;
        }

        return true;
    }

    private static List<int> FindStaticStoreGroups(byte[] data, byte[] pattern)
    {
        var positions = new List<int>();
        for (var position = 0; position <= data.Length - 26; position++)
        {
            if (Matches(data, position, pattern) &&
                Matches(data, position + 10, pattern) &&
                Matches(data, position + 20, pattern))
            {
                positions.Add(position);
            }
        }

        return positions;
    }

    private static List<int> FindInitTargetsValues(byte[] data, byte[] integerPattern)
    {
        var positions = new List<int>();
        for (var position = 0; position <= data.Length - 6; position++)
        {
            if (!Matches(data, position, integerPattern) ||
                !LocalStoreOpcodes.Contains(data[position + 5]) ||
                !HasNearbyHidefCheck(data, position))
            {
                continue;
            }

            positions.Add(position);
        }

        return positions;
    }

    private static bool HasNearbyHidefCheck(byte[] data, int position)
    {
        var start = Math.Max(0, position - 15);
        for (var index = start; index + 1 < position; index++)
        {
            if (data[index] == 0x17 && HidefBranchOpcodes.Contains(data[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(byte[] data, int offset, byte[] pattern)
    {
        return offset >= 0 &&
               offset <= data.Length - pattern.Length &&
               data.AsSpan(offset, pattern.Length).SequenceEqual(pattern);
    }

    private static PatchPlan Failed(string message)
    {
        return new PatchPlan(false, false, false, false, message, null);
    }
}
