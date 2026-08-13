using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class SyntaxServiceTests
{
    private readonly SyntaxService service = new();

    [Test]
    public void Parse_ValidExpression_ReturnsSyntaxTree()
    {
        var result = service.Parse(".name | upper");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.SyntaxTree, Is.Not.Null);
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [Test]
    public void Parse_UnknownFunctionName_ReturnsNoSyntaxErrors()
    {
        var result = service.Parse("this-function-does-not-exist(1)");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [Test]
    public void Parse_InvalidExpression_ReturnsSyntaxErrors()
    {
        var result = service.Parse("upper(");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SyntaxTree, Is.Null);
            Assert.That(result.Errors, Is.Not.Empty);
        });
    }
}
