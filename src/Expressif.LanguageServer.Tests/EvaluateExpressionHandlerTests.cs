using Expressif.LanguageServer.Core.Evaluation;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.JsonRpc;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class EvaluateExpressionHandlerTests
{
    [Test]
    public async Task Handle_DelegatesToEvaluationServiceAsync()
    {
        var expected = ExpressionEvaluationResult.Success("42");
        var evaluation = new Mock<IExpressionEvaluationService>();
        evaluation.Setup(service => service.Evaluate("add(2)", "40")).Returns(expected);
        var handler = new EvaluateExpressionHandler(evaluation.Object, Mock.Of<ISerializer>());

        var result = await handler.Handle("add(2)", "40", CancellationToken.None);

        Assert.That(result, Is.SameAs(expected));
        evaluation.Verify(service => service.Evaluate("add(2)", "40"), Times.Once);
    }
}
