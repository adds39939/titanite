using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class ShellTokenizerTests
{
    [Fact]
    public void SplitsOnWhitespace() =>
        Assert.Equal(["taskset", "-c", "0-7,16-23"], ShellTokenizer.Tokenize("taskset -c 0-7,16-23"));

    [Fact]
    public void KeepsQuotedWhitespaceInsideOneToken() =>
        Assert.Equal(["/home/adam/my games/run.sh"], ShellTokenizer.Tokenize("\"/home/adam/my games/run.sh\""));

    [Fact]
    public void TreatsSingleQuotesAsLiteral() =>
        Assert.Equal([@"a\b$c"], ShellTokenizer.Tokenize(@"'a\b$c'"));

    [Fact]
    public void ResolvesEscapesInsideDoubleQuotes() =>
        Assert.Equal(["say \"hi\""], ShellTokenizer.Tokenize("\"say \\\"hi\\\"\""));

    [Fact]
    public void LeavesNonEscapingBackslashesAloneInsideDoubleQuotes() =>
        Assert.Equal([@"C:\games"], ShellTokenizer.Tokenize("\"C:\\games\""));

    [Fact]
    public void ProducesAnEmptyTokenForEmptyQuotes() =>
        Assert.Equal([""], ShellTokenizer.Tokenize("\"\""));

    [Fact]
    public void JoinsQuotedRunsAgainstAdjacentText() =>
        Assert.Equal(["NAME=two words"], ShellTokenizer.Tokenize("NAME=\"two words\""));

    [Fact]
    public void ReadsAnUnterminatedQuoteToEndOfInput() =>
        Assert.Equal(["broken value"], ShellTokenizer.Tokenize("\"broken value"));

    [Theory]
    [InlineData("mangohud", "mangohud")]
    [InlineData("0-7,16-23", "0-7,16-23")]
    [InlineData("%command%", "%command%")]
    [InlineData("fps_limit=224,fps_limit_method=late", "fps_limit=224,fps_limit_method=late")]
    [InlineData("/home/adam/bin/ow-dlss", "/home/adam/bin/ow-dlss")]
    [InlineData("two words", "\"two words\"")]
    [InlineData("", "\"\"")]
    [InlineData("dxgi=n,b", "dxgi=n,b")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    public void QuotesOnlyWhereNeeded(string token, string expected) =>
        Assert.Equal(expected, ShellTokenizer.Quote(token));

    [Theory]
    [InlineData("two words")]
    [InlineData("a\"b")]
    [InlineData(@"back\slash")]
    [InlineData("dollar$sign")]
    [InlineData("semi;colon")]
    [InlineData("")]
    public void QuotingSurvivesATokenizeRoundTrip(string token) =>
        Assert.Equal([token], ShellTokenizer.Tokenize(ShellTokenizer.Quote(token)));
}
