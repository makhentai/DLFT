using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class ModArchiveInstallerTests : IDisposable
{
    private readonly string _sourceDir =
        Path.Combine(Path.GetTempPath(), "dlft-installer-src-" + Guid.NewGuid().ToString("N"));
    private readonly string _destDir =
        Path.Combine(Path.GetTempPath(), "dlft-installer-dst-" + Guid.NewGuid().ToString("N"));

    public ModArchiveInstallerTests()
    {
        Directory.CreateDirectory(_sourceDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sourceDir))
        {
            Directory.Delete(_sourceDir, recursive: true);
        }
        if (Directory.Exists(_destDir))
        {
            Directory.Delete(_destDir, recursive: true);
        }
    }

    [Fact]
    public void Install_ReturnsEmptySummary_WhenSourceHasNoArchives()
    {
        var reader = new FakeArchiveReader();
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => { });
        var output = new StringWriter();
        var confirmCalled = false;

        var summary = installer.Install(_sourceDir, _destDir, _ => { confirmCalled = true; return true; }, output);

        Assert.Equal(0, summary.TotalArchives);
        Assert.False(confirmCalled);
    }

    [Fact]
    public void Install_ExtractsAllArchives_WithoutAsking_WhenNoConflicts()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "mod-a.zip"), "stub");
        File.WriteAllText(Path.Combine(_sourceDir, "mod-b.7z"), "stub");

        var reader = new FakeArchiveReader
        {
            EntryPathsByArchive =
            {
                ["mod-a.zip"] = new[] { "BepInEx/plugins/ModA.dll" },
                ["mod-b.7z"] = new[] { "BepInEx/plugins/ModB.dll" },
            },
            ExtractResultsByArchive =
            {
                ["mod-a.zip"] = new ArchiveExtractResult(1, 0),
                ["mod-b.7z"] = new ArchiveExtractResult(1, 0),
            }
        };
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => { });
        var output = new StringWriter();

        var summary = installer.Install(_sourceDir, _destDir, _ => throw new InvalidOperationException("не должно спрашивать"), output);

        Assert.Equal(2, summary.TotalArchives);
        Assert.Equal(2, summary.ArchivesInstalled);
        Assert.Equal(2, summary.FilesWritten);
        Assert.Equal(0, summary.FilesSkipped);
        Assert.All(reader.ExtractCalls, call => Assert.False(call.OverwriteExisting));
    }

    [Fact]
    public void Install_AsksOnceForOverwrite_WhenConflictsExist_AndPassesAnswerToAllExtracts()
    {
        Directory.CreateDirectory(_destDir);
        File.WriteAllText(Path.Combine(_destDir, "ExistingMod.dll"), "old content");

        File.WriteAllText(Path.Combine(_sourceDir, "mod-a.zip"), "stub");
        File.WriteAllText(Path.Combine(_sourceDir, "mod-b.zip"), "stub");

        var reader = new FakeArchiveReader
        {
            EntryPathsByArchive =
            {
                ["mod-a.zip"] = new[] { "ExistingMod.dll" },
                ["mod-b.zip"] = new[] { "SomeOtherFile.dll" },
            },
            ExtractResultsByArchive =
            {
                ["mod-a.zip"] = new ArchiveExtractResult(1, 0),
                ["mod-b.zip"] = new ArchiveExtractResult(1, 0),
            }
        };
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => { });
        var output = new StringWriter();
        var confirmCallCount = 0;

        var summary = installer.Install(_sourceDir, _destDir, conflictCount =>
        {
            confirmCallCount++;
            Assert.Equal(1, conflictCount);
            return true;
        }, output);

        Assert.Equal(1, confirmCallCount);
        Assert.Equal(2, summary.ArchivesInstalled);
        Assert.All(reader.ExtractCalls, call => Assert.True(call.OverwriteExisting));
    }

    [Fact]
    public void Install_RecordsError_WhenOneArchiveThrows_ButContinuesWithOthers()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "broken.zip"), "stub");
        File.WriteAllText(Path.Combine(_sourceDir, "good.zip"), "stub");

        var reader = new FakeArchiveReader
        {
            EntryPathsByArchive =
            {
                ["broken.zip"] = new[] { "Broken.dll" },
                ["good.zip"] = new[] { "Good.dll" },
            },
            ExtractResultsByArchive =
            {
                ["good.zip"] = new ArchiveExtractResult(1, 0),
            },
            ThrowOnExtractFor = "broken.zip"
        };
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => { });
        var output = new StringWriter();

        var summary = installer.Install(_sourceDir, _destDir, _ => false, output);

        Assert.Equal(2, summary.TotalArchives);
        Assert.Equal(1, summary.ArchivesInstalled);
        Assert.Single(summary.Errors);
        Assert.Contains("broken.zip", summary.Errors[0]);
    }

    [Fact]
    public void Install_RetriesExtraction_AndSucceeds_WhenTransientErrorClearsUpBeforeAttemptsRunOut()
    {
        // Воспроизводит найденный на практике случай: запись файла архива
        // иногда кратковременно падает (например, из-за антивируса,
        // держащего файл открытым долю секунды) — повторная попытка почти
        // всегда проходит успешно.
        File.WriteAllText(Path.Combine(_sourceDir, "flaky.zip"), "stub");

        var reader = new FakeArchiveReader
        {
            EntryPathsByArchive = { ["flaky.zip"] = new[] { "Flaky.dll" } },
            ExtractResultsByArchive = { ["flaky.zip"] = new ArchiveExtractResult(1, 0) },
            FailExtractAttemptsRemaining = { ["flaky.zip"] = 2 }
        };
        var delayCalls = 0;
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => delayCalls++);
        var output = new StringWriter();

        var summary = installer.Install(_sourceDir, _destDir, _ => false, output);

        Assert.Equal(1, summary.ArchivesInstalled);
        Assert.Empty(summary.Errors);
        Assert.Equal(3, reader.ExtractCalls.Count(c => Path.GetFileName(c.ArchivePath) == "flaky.zip"));
        Assert.Equal(2, delayCalls);
    }

    [Fact]
    public void Install_RecordsError_WhenExtractionFailsOnEveryRetryAttempt()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "always-flaky.zip"), "stub");

        var reader = new FakeArchiveReader
        {
            EntryPathsByArchive = { ["always-flaky.zip"] = new[] { "X.dll" } },
            FailExtractAttemptsRemaining = { ["always-flaky.zip"] = int.MaxValue }
        };
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => { });
        var output = new StringWriter();

        var summary = installer.Install(_sourceDir, _destDir, _ => false, output);

        Assert.Single(summary.Errors);
        Assert.Equal(0, summary.ArchivesInstalled);
        // Ровно 3 попытки — не бесконечный ретрай и не одна попытка без повтора.
        Assert.Equal(3, reader.ExtractCalls.Count(c => Path.GetFileName(c.ArchivePath) == "always-flaky.zip"));
    }

    [Fact]
    public void Install_RecordsError_WhenListingEntriesThrows_ButContinuesWithOthers()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "broken.7z"), "not really an archive");
        File.WriteAllText(Path.Combine(_sourceDir, "good.zip"), "stub");

        var reader = new FakeArchiveReader
        {
            EntryPathsByArchive =
            {
                ["good.zip"] = new[] { "Good.dll" },
            },
            ExtractResultsByArchive =
            {
                ["good.zip"] = new ArchiveExtractResult(1, 0),
            },
            ThrowOnListFor = "broken.7z"
        };
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => { });
        var output = new StringWriter();

        var summary = installer.Install(_sourceDir, _destDir, _ => false, output);

        Assert.Equal(2, summary.TotalArchives);
        Assert.Equal(1, summary.ArchivesInstalled);
        Assert.Single(summary.Errors);
        Assert.Contains("broken.7z", summary.Errors[0]);
        Assert.DoesNotContain(reader.ExtractCalls, call => Path.GetFileName(call.ArchivePath) == "broken.7z");
    }

    [Fact]
    public void Install_PassesDocSkipPredicateToReader_AndDoesNotCountDocFileAsConflict_WhenSkipDocFilesIsTrue()
    {
        // README.md конфликтует с уже существующим файлом на диске, но при
        // skipDocFiles=true он вообще не должен ни распаковываться, ни
        // провоцировать вопрос "перезаписать?" — конфликтов нет ни одного.
        Directory.CreateDirectory(_destDir);
        File.WriteAllText(Path.Combine(_destDir, "README.md"), "old readme");

        File.WriteAllText(Path.Combine(_sourceDir, "mod-a.zip"), "stub");

        var reader = new FakeArchiveReader
        {
            EntryPathsByArchive = { ["mod-a.zip"] = new[] { "README.md", "BepInEx/plugins/ModA.dll" } },
            ExtractResultsByArchive = { ["mod-a.zip"] = new ArchiveExtractResult(1, 0, DocsSkipped: 1) },
        };
        var installer = new ModArchiveInstaller(reader, retryDelay: _ => { });
        var output = new StringWriter();

        var summary = installer.Install(
            _sourceDir, _destDir, _ => throw new InvalidOperationException("не должно спрашивать"), output,
            skipDocFiles: true);

        Assert.Equal(1, summary.ArchivesInstalled);
        Assert.Equal(1, summary.DocsSkipped);
        Assert.NotNull(reader.LastShouldSkipEntry);
        Assert.True(reader.LastShouldSkipEntry!("README.md"));
        Assert.False(reader.LastShouldSkipEntry!("BepInEx/plugins/ModA.dll"));
    }
}

internal sealed class FakeArchiveReader : IArchiveReader
{
    public Dictionary<string, IReadOnlyList<string>> EntryPathsByArchive { get; } = new();
    public Dictionary<string, ArchiveExtractResult> ExtractResultsByArchive { get; } = new();
    public Dictionary<string, int> FailExtractAttemptsRemaining { get; } = new();
    public string? ThrowOnExtractFor { get; set; }
    public string? ThrowOnListFor { get; set; }
    public List<(string ArchivePath, bool OverwriteExisting)> ExtractCalls { get; } = new();
    public Func<string, bool>? LastShouldSkipEntry { get; private set; }

    public IReadOnlyList<string> ListEntryPaths(string archivePath)
    {
        var name = Path.GetFileName(archivePath);
        if (name == ThrowOnListFor)
        {
            throw new InvalidOperationException($"не удалось открыть архив: {name}");
        }

        return EntryPathsByArchive.TryGetValue(name, out var paths) ? paths : Array.Empty<string>();
    }

    public ArchiveExtractResult ExtractTo(
        string archivePath, string destinationDirectory, bool overwriteExisting, Func<string, bool>? shouldSkipEntry = null)
    {
        var name = Path.GetFileName(archivePath);
        ExtractCalls.Add((archivePath, overwriteExisting));
        LastShouldSkipEntry = shouldSkipEntry;

        if (name == ThrowOnExtractFor)
        {
            throw new InvalidOperationException($"повреждённый архив: {name}");
        }

        if (FailExtractAttemptsRemaining.TryGetValue(name, out var remaining) && remaining > 0)
        {
            FailExtractAttemptsRemaining[name] = remaining - 1;
            throw new IOException($"временная ошибка доступа к файлу: {name}");
        }

        return ExtractResultsByArchive.TryGetValue(name, out var result)
            ? result
            : new ArchiveExtractResult(0, 0);
    }
}
