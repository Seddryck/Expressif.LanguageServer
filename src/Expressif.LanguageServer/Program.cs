using Expressif.LanguageServer.Handlers;
using Expressif.LanguageServer.Core.Completion;
using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.CodeActions;
using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Hover;
using Expressif.LanguageServer.Core.SignatureHelp;
using Expressif.LanguageServer.Core.SemanticTokens;
using Expressif.LanguageServer.Core.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Expressif.LanguageServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var server = await global::OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options => options
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .ConfigureLogging(logging =>
            {
                logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
            })
            .WithServices(services =>
            {
                services.AddSingleton<ISyntaxService, SyntaxService>();
                services.AddSingleton<IDocumentStore, DocumentStore>();
                services.AddSingleton<IFunctionCatalog, ExpressifFunctionCatalog>();
                services.AddSingleton<ICompletionService, CompletionService>();
                services.AddSingleton<IFunctionHoverService, FunctionHoverService>();
                services.AddSingleton<IFunctionSignatureHelpService, FunctionSignatureHelpService>();
                services.AddSingleton<ISemanticTokenService, SemanticTokenService>();
                services.AddSingleton<IFunctionLifecycleDiagnosticService, FunctionLifecycleDiagnosticService>();
                services.AddSingleton<IFunctionCodeActionService, FunctionCodeActionService>();
            })
            .WithHandler<TextDocumentSyncHandler>()
            .WithHandler<CompletionHandler>()
            .WithHandler<HoverHandler>()
            .WithHandler<SignatureHelpHandler>()
            .WithHandler<SemanticTokensHandler>()
            .WithHandler<CodeActionHandler>());

        await server.WaitForExit;
    }
}
