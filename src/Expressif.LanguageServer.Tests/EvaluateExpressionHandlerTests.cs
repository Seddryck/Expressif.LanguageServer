using Expressif.LanguageServer.Core.Evaluation;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class EvaluateExpressionHandlerTests
{
    [Test]
    public async Task Handle_DelegatesToEvaluationServiceAsync()
    {
        var expected = ExpressionEvaluationResult.Success("42");
        var evaluation = new Mock<IExpressionEvaluationService>();
        evaluation.Setup(service => service.Evaluate(
            "add(2)",
            "40",
            EvaluationInputFormat.Json,
            EvaluationOutputFormat.Json,
            new EvaluationOutputOptions(EvaluationOutputFormatting.Pretty, 4))).Returns(expected);
        var configuration = new Mock<ILanguageServerConfiguration>();
        configuration.Setup(config => config["expressif:output:formatting"]).Returns("pretty");
        configuration.Setup(config => config["expressif:output:indent"]).Returns("4");
        var handler = new EvaluateExpressionHandler(
            evaluation.Object,
            configuration.Object,
            Mock.Of<ISerializer>());

        var result = await handler.Handle(
            "add(2)",
            "40",
            EvaluationInputFormat.Json,
            EvaluationOutputFormat.Json,
            CancellationToken.None);

        Assert.That(result, Is.SameAs(expected));
        evaluation.Verify(service => service.Evaluate(
            "add(2)",
            "40",
            EvaluationInputFormat.Json,
            EvaluationOutputFormat.Json,
            new EvaluationOutputOptions(EvaluationOutputFormatting.Pretty, 4)), Times.Once);
    }
}
