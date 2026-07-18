using System.Text;
using Siphon.Media.Delivery;

namespace Siphon.Tests;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("a<b>c:d\"e/f\\g|h?i*j", "a b c d e f g h i j")]
    [InlineData("  Jahongir  Otajonov - O'rgatib qo'yma  ", "Jahongir Otajonov - O'rgatib qo'yma")]
    [InlineData("name...", "name")]
    [InlineData("Со мной воюет сирень", "Со мной воюет сирень")]
    public void Cleans_illegal_characters(string input, string expected) =>
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input));

    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("COM1")]
    public void Prefixes_reserved_names(string reserved) =>
        Assert.Equal("_" + reserved, FileNameSanitizer.Sanitize(reserved));

    [Fact]
    public void Empty_becomes_media()
    {
        Assert.Equal("media", FileNameSanitizer.Sanitize(""));
        Assert.Equal("media", FileNameSanitizer.Sanitize("???"));
    }

    [Fact]
    public void Truncates_long_utf8_titles_on_char_boundary()
    {
        var title = string.Concat(Enumerable.Repeat("g'oʻzal qoʻshiq ", 30));
        var result = FileNameSanitizer.Sanitize(title);
        Assert.True(Encoding.UTF8.GetByteCount(result) <= 180);
        Assert.NotEqual('�', result[^1]);
    }
}
