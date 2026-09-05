# Expressif Language Support for VS Code

This extension is a thin client for `Expressif.LanguageServer`. It registers `.expressif` files and forwards editor activity to the server over the Language Server Protocol. Parsing and every language feature remain in the server.

## Run an expression

Press `Ctrl+Enter` (`Cmd+Enter` on macOS), use the play button in the editor title, or run **Expressif: Run Expression** from the Command Palette. The command evaluates the current selection, or the whole document when nothing is selected. Closed expressions run immediately; expressions that require input prompt for an Expressif value, defaulting to `#null`. Results appear in the **Expressif Evaluation** output channel.

## Development

Prerequisites are Node.js, npm, the .NET 10 SDK, and VS Code.

```powershell
cd vscode-extension
npm install
npm run compile
```

Open the repository in VS Code and press **F5**. The extension development host uses the server path configured in `expressif.languageServer.path`. For an unpackaged development run, first publish the server with:

```powershell
node ./scripts/publish-server.mjs
```

Open a `.expressif` file in the development host to activate the extension. Server stderr and language-client traces are available in **View > Output > Expressif Language Server**. Set `expressifLanguageServer.trace.server` to `messages` or `verbose` for LSP tracing.

## Package and install

Run the following from this directory:

```powershell
npm install
npm run package
code --install-extension ./artifacts/expressif-language-support.vsix
```

Packaging publishes a self-contained server for the operating system and architecture on which the command runs, then embeds it in the VSIX. Build the VSIX on each target platform. A locally installed package therefore requires neither the .NET runtime nor a separate server installation.

To use a separately installed server, set `expressif.languageServer.path` to its absolute executable path and reload VS Code.
