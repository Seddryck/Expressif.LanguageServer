using Expressif.LanguageServer.Core.Functions;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class ExpressifFunctionCatalogTests
{
    [Test]
    public void Functions_AreReadFromExpressifMetadata()
    {
        var functions = new ExpressifFunctionCatalog().Functions;

        var upper = functions.Single(function => function.Name == "upper");
        Assert.Multiple(() =>
        {
            Assert.That(upper.Aliases, Does.Contain("text-to-upper"));
            Assert.That(upper.Category, Is.EqualTo("Text"));
            Assert.That(upper.Description, Is.Not.Empty);
        });
    }

    [Test]
    public void Functions_IncludeSignatureMetadataFromExpressifIntrospection()
    {
        var functions = new ExpressifFunctionCatalog().Functions;

        var add = functions.Single(function => function.Name == "add");
        Assert.Multiple(() =>
        {
            Assert.That(add.Parameters, Is.Not.Empty);
            Assert.That(add.Parameters.All(parameter => !string.IsNullOrWhiteSpace(parameter.Name)), Is.True);
            Assert.That(add.Parameters.All(parameter => !string.IsNullOrWhiteSpace(parameter.Description)), Is.True);
        });
    }
}
