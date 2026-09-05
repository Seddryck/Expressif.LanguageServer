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
            Assert.That(upper.Category, Is.EqualTo("text/casing"));
            Assert.That(upper.Description, Is.Not.Empty);
        });
    }

    [Test]
    public void Functions_IncludePublicPredicatesAndTheirAliases()
    {
        var functions = new ExpressifFunctionCatalog().Functions;

        var predicate = functions.Single(function => function.Name == "is-lower-case");
        Assert.Multiple(() =>
        {
            Assert.That(predicate.Aliases, Does.Contain("lower-case"));
            Assert.That(predicate.Category, Is.EqualTo("text"));
            Assert.That(predicate.Description, Is.Not.Empty);
        });
    }

    [Test]
    public void Functions_DeprecatedAppend_ExposesLifecycleMetadataAndUnsafeReplacement()
    {
        var function = new ExpressifFunctionCatalog().Functions.Single(item => item.Name == "append");

        Assert.Multiple(() =>
        {
            Assert.That(function.Deprecated, Is.True);
            Assert.That(function.Replacement, Is.EqualTo("suffix"));
            Assert.That(function.Sunset, Is.EqualTo("3.0"));
            Assert.That(function.SafeDirectReplacement, Is.False);
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

    [Test]
    public void Functions_IncludeVariadicMetadataFromExpressifIntrospection()
    {
        var functions = new ExpressifFunctionCatalog().Functions;

        var coalesce = functions.Single(function => function.Name == "coalesce");
        var expressions = coalesce.Parameters.Single();
        Assert.Multiple(() =>
        {
            Assert.That(expressions.Variadic, Is.True);
            Assert.That(expressions.MinimumCardinality, Is.EqualTo(2));
            Assert.That(expressions.Description, Does.Contain("Two or more"));
        });
    }
}
