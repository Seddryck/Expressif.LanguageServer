using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class ImplicitBindingMigrationServiceTests
{
    private readonly ImplicitBindingMigrationService service = new();

    [TestCase("{1, 2, 5} | adjacent(subtract)", "adjacent", "subtract", "~subtract")]
    [TestCase("{1, 2, 5} | adjacent(greater-than)", "adjacent", "greater-than", "~greater-than")]
    [TestCase("5 | map-over(subtract, {10, 11})", "map-over", "subtract", "subtract~")]
    [TestCase("5 | map-with(subtract, {10, 11})", "map-with", "subtract", "~subtract")]
    public void GetMigrations_ResolvedLegacyBinding_UsesRuntimeContract(
        string source, string expectedOperator, string expectedCallable, string expectedReplacement)
    {
        var migration = service.GetMigrations(Parse(source)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(migration.Operator, Is.EqualTo(expectedOperator));
            Assert.That(migration.Callable, Is.EqualTo(expectedCallable));
            Assert.That(migration.Code, Is.EqualTo("implicit-tuple-binding"));
            Assert.That(migration.Message, Does.Contain("Implicit argument injection"));
            Assert.That(source.Substring(migration.Start, migration.Length), Is.EqualTo(expectedCallable));
            Assert.That(migration.Replacements[0].NewText, Is.EqualTo(expectedReplacement));
            Assert.That(migration.Replacements[0].Preferred, Is.True);
        });
    }

    [TestCase("{1, 2, 5} | adjacent(subtract)", "$1 | subtract($0)")]
    public void GetMigrations_PairBinding_OffersExplicitExpressionAlternative(
        string source, string expectedReplacement)
    {
        var replacement = service.GetMigrations(Parse(source)).Single().Replacements[1];

        Assert.That((replacement.NewText, replacement.Preferred),
            Is.EqualTo((expectedReplacement, false)));
    }

    [Test]
    public void GetMigrations_ChunkWhileChangedContext_ReportsButWithholdsUnsafeFix()
    {
        const string source = "{1, 2, 5} | chunk-while(subtract | is-less-than(2))";

        var migration = service.GetMigrations(Parse(source)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(migration.Operator, Is.EqualTo("chunk-while"));
            Assert.That(migration.Callable, Is.EqualTo("subtract"));
            Assert.That(migration.Replacements, Is.Empty);
        });
    }

    [Test]
    public void GetMigrations_AliasAndFormatting_PreserveCallableIdentityAndSurroundingText()
    {
        const string source = "{1, 2, 5} | adjacent( /* pair */ numeric-to-subtract )";

        var migration = service.GetMigrations(Parse(source)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(migration.Callable, Is.EqualTo("numeric-to-subtract"));
            Assert.That(source.Substring(migration.Start, migration.Length), Is.EqualTo("numeric-to-subtract"));
            Assert.That(migration.Replacements.Select(replacement => replacement.NewText),
                Is.EqualTo(new[] { "~numeric-to-subtract", "$1 | numeric-to-subtract($0)" }));
        });
    }

    [TestCase("adjacent(arity)")]
    [TestCase("reduce(subtract)")]
    [TestCase("split-while(greater-than)")]
    [TestCase("{1, 2, 5} | adjacent(~subtract)")]
    [TestCase("5 | map-over(subtract~, {10, 11})")]
    [TestCase("\"adjacent(subtract)\"")]
    public void GetMigrations_NonDeprecatedUsage_ReturnsNone(string source)
        => Assert.That(service.GetMigrations(Parse(source)), Is.Empty);

    [TestCase("adjacent(subtract)")]
    [TestCase("{} | adjacent(subtract)")]
    [TestCase("{T(1, 2), T(3, 4)} | adjacent(subtract)")]
    [TestCase("5 | map-over(subtract, {T(1, 2, 3)})")]
    public void GetMigrations_UnprovenRewrite_ReportsDeprecationWithoutCodeAction(string source)
    {
        var migration = service.GetMigrations(Parse(source)).Single();

        Assert.That(migration.Replacements, Is.Empty);
    }

    [Test]
    public void GetMigrations_NullsAndNesting_RetainPreciseCallableRange()
    {
        const string source = "coalesce({1, #null, 3} | adjacent(subtract), {})";

        var migration = service.GetMigrations(Parse(source)).Single();

        Assert.That(migration.Start, Is.EqualTo(source.IndexOf("subtract", StringComparison.Ordinal)));
        Assert.That(migration.Replacements, Is.Not.Empty);
    }

    [Test]
    public void GetMigrations_UnicodeBeforeCallable_UsesUtf16SourceOffset()
    {
        const string source = "\"é\" | map-with(subtract, {10, 11})";

        var migration = service.GetMigrations(Parse(source)).Single();

        Assert.That(migration.Start, Is.EqualTo(source.IndexOf("subtract", StringComparison.Ordinal)));
        Assert.That(source.Substring(migration.Start, migration.Length), Is.EqualTo("subtract"));
    }

    private static Expressif.Syntax.RootExpressionSyntax Parse(string text)
    {
        var result = new SyntaxService().Parse(text);
        Assert.That(result.SyntaxTree, Is.Not.Null, string.Join(Environment.NewLine, result.Errors));
        return result.SyntaxTree!;
    }
}
