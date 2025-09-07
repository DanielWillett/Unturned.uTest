using NUnit.Framework;
using uTest;
using Assert = NUnit.Framework.Assert;

namespace uTest_Test;
internal class EscaperTests
{
    public TextEscaper StringLiteralEscaper { get; } = new TextEscaper('\r', '\n', '\t', '\v', '\\', '\"', '\0');

    [NUnit.Framework.Test]
    [TestCase("Basic", "Basic")]
    [TestCase("", "")]
    [TestCase(" ", " ")]
    [TestCase("\n", @"\n")]
    [TestCase("\r", @"\r")]
    [TestCase("\t", @"\t")]
    [TestCase("\v", @"\v")]
    [TestCase("\\", @"\\")]
    [TestCase("\"", @"\""")]
    [TestCase("\0", @"\0")]
    [TestCase("Full \nTest st\rring \\\\what woah", @"Full \nTest st\rring \\\\what woah")]
    [TestCase("\rtest\t", @"\rtest\t")]
    public void CheckEscape(string begin, string expected)
    {
        string escaped = StringLiteralEscaper.Escape(begin);

        Assert.That(escaped, Is.EqualTo(expected));
        Assert.That(StringLiteralEscaper.Unescape(escaped), Is.EqualTo(begin));
    }
}
