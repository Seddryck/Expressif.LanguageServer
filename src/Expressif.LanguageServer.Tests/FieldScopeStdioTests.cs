using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class FieldScopeStdioTests
{
    [Test]
    public async Task Server_AdvertisesAndDispatchesDocumentHighlightsAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync(token);
        try
        {
            var input = process.StandardInput.BaseStream;
            var output = process.StandardOutput.BaseStream;
            await SendAsync(input, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"processId":null,"rootUri":null,"capabilities":{"textDocument":{"documentHighlight":{"dynamicRegistration":false},"hover":{"dynamicRegistration":false},"synchronization":{"dynamicRegistration":false}}}}}""", token);
            using var initialized = await ResponseAsync(output, 1, token);
            var provider = initialized.RootElement.GetProperty("result").GetProperty("capabilities").GetProperty("documentHighlightProvider");
            Assert.That(provider.ValueKind, Is.AnyOf(JsonValueKind.True, JsonValueKind.Object));
            await SendAsync(input, """{"jsonrpc":"2.0","method":"initialized","params":{}}""", token);
            await SendAsync(input, """{"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///scope.expr","languageId":"expressif","version":1,"text":".a.b"}}}""", token);
            await SendAsync(input, """{"jsonrpc":"2.0","id":2,"method":"textDocument/documentHighlight","params":{"textDocument":{"uri":"file:///scope.expr"},"position":{"line":0,"character":2}}}""", token);
            using var highlights = await ResponseAsync(output, 2, token);
            var highlight = highlights.RootElement.GetProperty("result")[0];
            Assert.That(highlight.GetProperty("kind").GetInt32(), Is.EqualTo(1));
            Assert.That(highlight.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32(), Is.Zero);
            Assert.That(highlight.GetProperty("range").GetProperty("end").GetProperty("character").GetInt32(), Is.EqualTo(2));
            await SendAsync(input, """{"jsonrpc":"2.0","id":3,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///scope.expr"},"position":{"line":0,"character":2}}}""", token);
            using var hover = await ResponseAsync(output, 3, token);
            Assert.That(hover.RootElement.GetProperty("result").GetProperty("contents").GetProperty("value").GetString(),
                Does.Contain("not a field declaration"));
            await SendAsync(input, """{"jsonrpc":"2.0","id":4,"method":"shutdown","params":null}""", token);
            using var shutdown = await ResponseAsync(output, 4, token);
            await SendAsync(input, """{"jsonrpc":"2.0","method":"exit","params":null}""", token);
            await process.WaitForExitAsync(token);
            Assert.That(process.ExitCode, Is.Zero, await errors);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }

    private static async Task SendAsync(Stream stream, string json, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"), token);
        await stream.WriteAsync(body, token);
        await stream.FlushAsync(token);
    }

    private static async Task<JsonDocument> ResponseAsync(Stream stream, int id, CancellationToken token)
    {
        while (true)
        {
            var header = new StringBuilder();
            var next = new byte[1];
            while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                await stream.ReadExactlyAsync(next, token);
                header.Append((char)next[0]);
            }
            var length = int.Parse(header.ToString().Split("\r\n")
                .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                .Split(':')[1], System.Globalization.CultureInfo.InvariantCulture);
            var body = new byte[length];
            await stream.ReadExactlyAsync(body, token);
            var message = JsonDocument.Parse(body);
            if (message.RootElement.TryGetProperty("id", out var responseId) &&
                responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
                return message;
            message.Dispose();
        }
    }
}
