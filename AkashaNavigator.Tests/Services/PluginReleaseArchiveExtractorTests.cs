using System.IO;
using System.IO.Compression;
using System.Text;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginReleaseArchiveExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"AkashaNavigator.ReleaseArchiveTests.{Guid.NewGuid():N}");

    [Fact]
    public void Extract_AcceptsSingleTopLevelPluginDirectory()
    {
        var archivePath = CreateArchive(
            ("sample/manifest.json", "{}"),
            ("sample/frontend/main.js", "main"),
            ("sample/runtime/worker.exe", "worker"));
        var extractionRoot = Path.Combine(_root, "extract");

        var pluginDirectory =
            PluginReleaseArchiveExtractor.Extract(
                archivePath,
                extractionRoot);

        Assert.Equal(
            Path.Combine(extractionRoot, "sample"),
            pluginDirectory);
        Assert.True(
            File.Exists(
                Path.Combine(
                    pluginDirectory,
                    "runtime",
                    "worker.exe")));
    }

    [Fact]
    public void Extract_RejectsPathTraversalWithoutWritingOutsideStaging()
    {
        var archivePath = CreateArchive(
            ("manifest.json", "{}"),
            ("../escaped.txt", "unsafe"));
        var extractionRoot = Path.Combine(_root, "extract");

        var exception = Assert.Throws<InvalidDataException>(
            () => PluginReleaseArchiveExtractor.Extract(
                archivePath,
                extractionRoot));

        Assert.Contains("不安全路径", exception.Message);
        Assert.False(
            File.Exists(
                Path.Combine(_root, "escaped.txt")));
    }

    [Fact]
    public void Extract_RejectsWindowsReparsePointEntry()
    {
        Directory.CreateDirectory(_root);
        var archivePath = Path.Combine(_root, "reparse.zip");
        using (var archive = ZipFile.Open(
                   archivePath,
                   ZipArchiveMode.Create))
        {
            var manifest = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifest.Open()))
            {
                writer.Write("{}");
            }

            var reparseEntry = archive.CreateEntry("runtime/worker.exe");
            reparseEntry.ExternalAttributes =
                (int)FileAttributes.ReparsePoint;
            using var stream = reparseEntry.Open();
            stream.WriteByte(1);
        }

        var exception = Assert.Throws<InvalidDataException>(
            () => PluginReleaseArchiveExtractor.Extract(
                archivePath,
                Path.Combine(_root, "extract")));

        Assert.Contains("重解析点", exception.Message);
    }

    [Fact]
    public void Extract_RejectsMultipleCatalogManifests()
    {
        var archivePath = CreateArchive(
            ("first/manifest.json", "{}"),
            ("second/manifest.json", "{}"));

        var exception = Assert.Throws<InvalidDataException>(
            () => PluginReleaseArchiveExtractor.Extract(
                archivePath,
                Path.Combine(_root, "extract")));

        Assert.Contains("多个 manifest.json", exception.Message);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    private string CreateArchive(
        params (string Path, string Content)[] files)
    {
        Directory.CreateDirectory(_root);
        var archivePath = Path.Combine(
            _root,
            $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(
            archivePath,
            ZipArchiveMode.Create);
        foreach (var file in files)
        {
            var entry = archive.CreateEntry(file.Path);
            using var writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            writer.Write(file.Content);
        }

        return archivePath;
    }
}
