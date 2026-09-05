import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import {
  Executable,
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

interface EvaluationResult {
  succeeded: boolean;
  requiresInput: boolean;
  value?: string;
  error?: string;
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const outputChannel = vscode.window.createOutputChannel('Expressif Language Server');
  const evaluationChannel = vscode.window.createOutputChannel('Expressif Evaluation');
  context.subscriptions.push(outputChannel, evaluationChannel);

  const executable = resolveServerExecutable(context);
  const serverOptions: ServerOptions = {
    run: executable,
    debug: executable
  };
  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'expressif' }],
    outputChannel,
    traceOutputChannel: outputChannel
  };

  client = new LanguageClient(
    'expressifLanguageServer',
    'Expressif Language Server',
    serverOptions,
    clientOptions
  );

  context.subscriptions.push(client);
  context.subscriptions.push(vscode.commands.registerCommand(
    'expressif.runExpression',
    () => runExpression(evaluationChannel)
  ));
  outputChannel.appendLine(`Starting ${executable.command}`);

  try {
    await client.start();
  } catch (error) {
    outputChannel.show(true);
    throw error;
  }
}

async function runExpression(outputChannel: vscode.OutputChannel): Promise<void> {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== 'expressif') {
    await vscode.window.showErrorMessage('Open an Expressif document to run an expression.');
    return;
  }

  const expression = editor.selection.isEmpty
    ? editor.document.getText()
    : editor.document.getText(editor.selection);
  if (!expression.trim()) {
    await vscode.window.showErrorMessage('The expression is empty.');
    return;
  }

  if (!client) {
    await vscode.window.showErrorMessage('Expressif Language Server is not running.');
    return;
  }

  try {
    let input: string | undefined;
    let result = await client.sendRequest<EvaluationResult>('workspace/executeCommand', {
      command: 'expressif.evaluateExpression',
      arguments: [expression]
    });
    if (result.requiresInput) {
      input = await vscode.window.showInputBox({
        title: 'Run Expressif Expression',
        prompt: 'Enter an Expressif value to pass to the expression',
        placeHolder: 'Examples: 42, "text", {name := "Ada"}, {1, 2, 3}',
        value: '#null',
        ignoreFocusOut: true
      });
      if (input === undefined) {
        return;
      }

      result = await client.sendRequest<EvaluationResult>('workspace/executeCommand', {
        command: 'expressif.evaluateExpression',
        arguments: [expression, input]
      });
    }
    if (!result.succeeded) {
      await vscode.window.showErrorMessage(`Expressif evaluation failed: ${result.error ?? 'Unknown error.'}`);
      return;
    }

    outputChannel.appendLine(`> ${expression.trim()}`);
    if (input !== undefined) {
      outputChannel.appendLine(`Input: ${input}`);
    }
    outputChannel.appendLine(`Result: ${result.value ?? ''}`);
    outputChannel.appendLine('');
    outputChannel.show(true);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    await vscode.window.showErrorMessage(`Expressif evaluation failed: ${message}`);
  }
}

export async function deactivate(): Promise<void> {
  if (client) {
    await client.stop();
    client = undefined;
  }
}

function resolveServerExecutable(context: vscode.ExtensionContext): Executable {
  const configuredPath = vscode.workspace
    .getConfiguration('expressif.languageServer')
    .get<string>('path', '')
    .trim();

  const command = configuredPath || context.asAbsolutePath(
    path.join('server', platformRuntimeIdentifier(), serverFileName())
  );

  if (!path.isAbsolute(command)) {
    throw new Error('expressif.languageServer.path must be an absolute path.');
  }
  if (!fs.existsSync(command)) {
    throw new Error(
      `Expressif.LanguageServer was not found at "${command}". ` +
      'Build the VSIX with npm run package or configure expressif.languageServer.path.'
    );
  }

  return {
    command,
    transport: TransportKind.stdio,
    options: { cwd: path.dirname(command) }
  };
}

function platformRuntimeIdentifier(): string {
  const architecture = process.arch === 'arm64' ? 'arm64' : 'x64';
  switch (process.platform) {
    case 'win32': return `win-${architecture}`;
    case 'linux': return `linux-${architecture}`;
    case 'darwin': return `osx-${architecture}`;
    default: throw new Error(`Unsupported platform: ${process.platform}/${process.arch}`);
  }
}

function serverFileName(): string {
  return process.platform === 'win32'
    ? 'Expressif-LanguageServer.exe'
    : 'Expressif-LanguageServer';
}
