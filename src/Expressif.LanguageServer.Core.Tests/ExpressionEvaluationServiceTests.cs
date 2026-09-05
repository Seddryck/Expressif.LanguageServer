using Expressif.LanguageServer.Core.Evaluation;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class ExpressionEvaluationServiceTests
{
    private readonly ExpressionEvaluationService service = new();

    [Test]
    public void Evaluate_ScalarInput_ReturnsFormattedResult()
    {
        var result = service.Evaluate("add(2)", "40");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value, Is.EqualTo("42"));
            Assert.That(result.Error, Is.Null);
        });
    }

    [Test]
    public void Evaluate_RecordInput_ReturnsFormattedResult()
    {
        var result = service.Evaluate(".name | upper", "{name := \"Ada\"}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value, Is.EqualTo("ADA"));
            Assert.That(result.Error, Is.Null);
        });
    }

    [Test]
    public void Evaluate_InvalidExpression_ReturnsFailure()
    {
        var result = service.Evaluate("does-not-exist", "null");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Is.Not.Empty);
        });
    }

    [Test]
    public void Evaluate_EmptyExpression_ReturnsFailure()
    {
        var result = service.Evaluate("  ", "null");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo("The expression is empty."));
        });
    }
}
