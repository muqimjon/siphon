using System.IO.Compression;

namespace Siphon.Media.Delivery;

public static class ZipPackager
{
    public static string Pack(string sourceDir, IReadOnlyList<string> files, string zipBaseName)
    {
        var zipPath = Path.Combine(sourceDir, zipBaseName + ".zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in files)
            archive.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.NoCompression);
        return zipPath;
    }
}
