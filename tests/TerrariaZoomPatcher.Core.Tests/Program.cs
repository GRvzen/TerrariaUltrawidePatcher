using TerrariaZoomPatcher.Core;

var tests = new (string Name, Action Run)[]
{
    ("standard patch enables widescreen only", StandardPatchEnablesWidescreenOnly),
    ("8K patch raises all expected limits", EightKPatchRaisesAllLimits),
    ("already patched file is idempotent", ReapplyingPatchMakesNoChanges),
    ("unsupported 8K file remains unchanged", UnsupportedEightKFileRemainsUnchanged),
    ("ambiguous widescreen signatures are rejected", AmbiguousSignaturesAreRejected),
    ("file patch creates backup and can restore", FilePatchCreatesBackupAndRestores),
    ("existing backup is never overwritten", ExistingBackupIsNeverOverwritten)
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} checks passed.");
return failures == 0 ? 0 : 1;

static void StandardPatchEnablesWidescreenOnly()
{
    var source = CreateSyntheticTerrariaBytes(includeEightKTargets: true);
    var plan = TerrariaPatchEngine.CreatePlan(source, enableEightK: false);

    Assert(plan.Success, plan.Message);
    Assert(plan.HasChanges, "Expected a change to be planned.");
    Assert(plan.WidescreenEnabled, "Widescreen support was not enabled.");
    Assert(plan.PatchedBytes is not null, "Patched bytes were not returned.");
    Assert(plan.PatchedBytes![30] == 0x17, "SupportWideScreen was not changed to true.");
    Assert(plan.PatchedBytes[102] == 0x10, "The default mode changed a 4096 limit.");
    Assert(source[30] == 0x16, "The original input buffer was modified.");
}

static void EightKPatchRaisesAllLimits()
{
    var source = CreateSyntheticTerrariaBytes(includeEightKTargets: true);
    var plan = TerrariaPatchEngine.CreatePlan(source, enableEightK: true);

    Assert(plan.Success, plan.Message);
    Assert(plan.HasChanges, "Expected the 8K patch to change the file.");
    var patched = plan.PatchedBytes!;
    Assert(patched[30] == 0x17, "SupportWideScreen was not enabled.");
    Assert(patched[102] == 0x20, "The first graphics limit was not raised.");
    Assert(patched[112] == 0x20, "The second graphics limit was not raised.");
    Assert(patched[122] == 0x20, "The third graphics limit was not raised.");
    Assert(patched[202] == 0x20, "The render-target limit was not raised.");
}

static void ReapplyingPatchMakesNoChanges()
{
    var source = CreateSyntheticTerrariaBytes(includeEightKTargets: true);
    var firstPlan = TerrariaPatchEngine.CreatePlan(source, enableEightK: true);
    Assert(firstPlan.Success, firstPlan.Message);

    var secondPlan = TerrariaPatchEngine.CreatePlan(firstPlan.PatchedBytes!, enableEightK: true);
    Assert(secondPlan.Success, secondPlan.Message);
    Assert(!secondPlan.HasChanges, "Reapplying the patch should be a no-op.");
}

static void UnsupportedEightKFileRemainsUnchanged()
{
    var source = CreateSyntheticTerrariaBytes(includeEightKTargets: false);
    var original = source.ToArray();
    var plan = TerrariaPatchEngine.CreatePlan(source, enableEightK: true);

    Assert(!plan.Success, "An incomplete 8K signature should be rejected.");
    Assert(plan.PatchedBytes is null, "A failed plan must not return writable bytes.");
    Assert(source.SequenceEqual(original), "The input buffer was modified on failure.");
}

static void AmbiguousSignaturesAreRejected()
{
    var source = CreateSyntheticTerrariaBytes(includeEightKTargets: true);
    Array.Copy(source, 20, source, 400, 16);
    var original = source.ToArray();

    var plan = TerrariaPatchEngine.CreatePlan(source, enableEightK: false);

    Assert(!plan.Success, "Duplicate target signatures should be rejected.");
    Assert(source.SequenceEqual(original), "The input buffer was modified on failure.");
}

static void FilePatchCreatesBackupAndRestores()
{
    var originalBytes = CreateSyntheticTerrariaBytes(includeEightKTargets: false);
    var directory = Path.Combine(
        Path.GetTempPath(),
        "TerrariaPatchTests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);

    try
    {
        var exePath = Path.Combine(directory, "Terraria.exe");
        var backupPath = TerrariaFilePatcher.GetBackupPath(exePath);
        File.WriteAllBytes(exePath, originalBytes);

        var patchResult = TerrariaFilePatcher.Apply(exePath, enableEightK: false);
        Assert(patchResult.Success, patchResult.Message);
        Assert(File.Exists(backupPath), "A backup file was not created.");
        Assert(
            File.ReadAllBytes(backupPath).SequenceEqual(originalBytes),
            "The backup does not match the original file.");
        Assert(
            File.ReadAllBytes(exePath)[30] == 0x17,
            "The patched executable does not contain the widescreen flag.");

        var restoreResult = TerrariaFilePatcher.RestoreBackup(exePath);
        Assert(restoreResult.Success, restoreResult.Message);
        Assert(
            File.ReadAllBytes(exePath).SequenceEqual(originalBytes),
            "Restoration did not restore the original file.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void ExistingBackupIsNeverOverwritten()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        "TerrariaPatchTests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);

    try
    {
        var exePath = Path.Combine(directory, "Terraria.exe");
        var backupPath = TerrariaFilePatcher.GetBackupPath(exePath);
        var originalBytes = CreateSyntheticTerrariaBytes(includeEightKTargets: false);
        var existingBackup = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        File.WriteAllBytes(exePath, originalBytes);
        File.WriteAllBytes(backupPath, existingBackup);

        var result = TerrariaFilePatcher.Apply(exePath, enableEightK: false);
        Assert(result.Success, result.Message);
        Assert(
            File.ReadAllBytes(backupPath).SequenceEqual(existingBackup),
            "An existing backup was overwritten.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static byte[] CreateSyntheticTerrariaBytes(bool includeEightKTargets)
{
    var data = new byte[512];

    // _renderTargetMaxSize = 2048, followed by SupportWideScreen = false.
    var widescreen = new byte[]
    {
        0x20, 0x00, 0x08, 0x00, 0x00, 0x80, 0x11, 0x22, 0x33, 0x04,
        0x16, 0x80, 0x44, 0x55, 0x66, 0x04
    };
    Array.Copy(widescreen, 0, data, 20, widescreen.Length);

    if (!includeEightKTargets)
    {
        return data;
    }

    var fourKStore = new byte[]
    {
        0x20, 0x00, 0x10, 0x00, 0x00, 0x80, 0x11, 0x22, 0x33, 0x04
    };
    for (var index = 0; index < 3; index++)
    {
        Array.Copy(fourKStore, 0, data, 100 + index * 10, fourKStore.Length);
    }

    data[190] = 0x17;
    data[191] = 0x33;
    new byte[] { 0x20, 0x00, 0x10, 0x00, 0x00, 0x0A }
        .CopyTo(data, 200);

    return data;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
