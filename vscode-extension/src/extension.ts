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

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const outputChannel = vscode.window.createOutputChannel('Expressif Language Server');
  context.subscriptions.push(outputChannel);

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
  outputChannel.appendLine(`Starting ${executable.command}`);

  try {
    await client.start();
  } catch (error) {
    outputChannel.show(true);
    throw error;
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
