using System.IO.Compression;
using System.Text;

namespace NPUtil.Tests;

public static class SandboxHelper
{
    public static string CreateTestSandbox(string testName)
    {
        var sandboxDir = Path.Combine(Path.GetTempPath(), "NPUtil_Sandbox_" + testName + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(sandboxDir);
        return sandboxDir;
    }

    public static void CleanUpSandbox(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch { /* ignore */ }
    }

    public static string CreateTextFile(string dir, string fileName, string content)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    public static string CreateBinaryFile(string dir, string fileName, byte[] bytes)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static string CreateMockDocx(string dir, string fileName, string bodyText)
    {
        var path = Path.Combine(dir, fileName);
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
    <w:body>
        <w:p>
            <w:r><w:t>{bodyText}</w:t></w:r>
        </w:p>
    </w:body>
</w:document>");
        }
        return path;
    }

    public static string CreateMockXlsx(string dir, string fileName, string cellText)
    {
        var path = Path.Combine(dir, fileName);
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("xl/sharedStrings.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
    <si><t>{cellText}</t></si>
</sst>");
        }
        return path;
    }
}
