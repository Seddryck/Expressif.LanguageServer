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
let previousInput: EvaluationInput | undefined;
let lastDataEditor: vscode.TextEditor | undefined;

interface EvaluationInput {
  value?: string;
  description: string;
}

interface EvaluationInputQuickPickItem extends vscode.QuickPickItem {
  inputKind: 'none' | 'literal' | 'file' | 'editor' | 'selection' | 'previous';
}

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
  rememberDataEditor(vscode.window.activeTextEditor);
  context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(rememberDataEditor));

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
    const selectedInput = await selectEvaluationInput(editor);
    if (!selectedInput) {
      return;
    }

    const result = await client.sendRequest<EvaluationResult>('workspace/executeCommand', {
      command: 'expressif.evaluateExpression',
      arguments: [expression, selectedInput.value]
    });
    if (result.requiresInput) {
      await vscode.window.showErrorMessage(
        'This expression requires input. Run it again and choose an input source.'
      );
      return;
    }
    if (!result.succeeded) {
      await vscode.window.showErrorMessage(`Expressif evaluation failed: ${result.error ?? 'Unknown error.'}`);
      return;
    }

    if (selectedInput.value !== undefined) {
      previousInput = selectedInput;
    }

    outputChannel.appendLine(`> ${expression.trim()}`);
    if (selectedInput.value !== undefined) {
      outputChannel.appendLine(`Input (${selectedInput.description}): ${selectedInput.value}`);
    }
    outputChannel.appendLine(`Result: ${result.value ?? ''}`);
    outputChannel.appendLine('');
    outputChannel.show(true);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    await vscode.window.showErrorMessage(`Expressif evaluation failed: ${message}`);
  }
}

async function selectEvaluationInput(
  expressionEditor: vscode.TextEditor
): Promise<EvaluationInput | undefined> {
  const items: EvaluationInputQuickPickItem[] = [
    { label: 'No input', inputKind: 'none' },
    { label: 'Enter a literal value', inputKind: 'literal' },
    { label: 'Select a JSON or CSV file', inputKind: 'file' },
    { label: 'Use the active JSON/CSV editor', inputKind: 'editor' },
    { label: 'Use the current selection', inputKind: 'selection' },
    {
      label: 'Reuse the previous input',
      description: previousInput ? previousInput.description : 'No previous input',
      inputKind: 'previous'
    }
  ];

  while (true) {
    const choice = await vscode.window.showQuickPick(items, {
      title: 'Run Expressif Expression',
      placeHolder: 'Choose the input for this evaluation',
      ignoreFocusOut: true
    });
    if (!choice) {
      return undefined;
    }

    switch (choice.inputKind) {
      case 'none':
        return { value: undefined, description: 'none' };
      case 'literal': {
        const value = await vscode.window.showInputBox({
          title: 'Run Expressif Expression',
          prompt: 'Enter an Expressif literal value',
          placeHolder: 'Examples: 42, "text", {name := "Ada"}, {1, 2, 3}',
          value: '#null',
          ignoreFocusOut: true
        });
        return value === undefined ? undefined : { value, description: 'literal' };
      }
      case 'file': {
        const selected = await vscode.window.showOpenDialog({
          title: 'Select evaluation input',
          canSelectMany: false,
          canSelectFiles: true,
          canSelectFolders: false,
          filters: { 'JSON or CSV': ['json', 'csv'] },
          openLabel: 'Use as input'
        });
        if (!selected?.[0]) {
          return undefined;
        }
        const bytes = await vscode.workspace.fs.readFile(selected[0]);
        return {
          value: new TextDecoder().decode(bytes),
          description: path.basename(selected[0].fsPath)
        };
      }
      case 'editor': {
        const dataEditor = findDataEditor(expressionEditor);
        if (dataEditor) {
          return {
            value: dataEditor.document.getText(),
            description: path.basename(dataEditor.document.fileName)
          };
        }
        await vscode.window.showWarningMessage('No JSON or CSV editor is currently open.');
        break;
      }
      case 'selection': {
        const dataEditor = findDataEditor(expressionEditor);
        if (dataEditor && !dataEditor.selection.isEmpty) {
          return {
            value: dataEditor.document.getText(dataEditor.selection),
            description: `selection from ${path.basename(dataEditor.document.fileName)}`
          };
        }
        await vscode.window.showWarningMessage(
          'There is no current selection in an open JSON or CSV editor.'
        );
        break;
      }
      case 'previous':
        if (previousInput) {
          return previousInput;
        }
        await vscode.window.showWarningMessage('There is no previous evaluation input to reuse.');
        break;
    }
  }
}

function findDataEditor(expressionEditor: vscode.TextEditor): vscode.TextEditor | undefined {
  if (lastDataEditor && lastDataEditor !== expressionEditor && isJsonOrCsv(lastDataEditor.document)) {
    return lastDataEditor;
  }
  return vscode.window.visibleTextEditors.find(editor =>
    editor !== expressionEditor && isJsonOrCsv(editor.document)
  );
}

function rememberDataEditor(editor: vscode.TextEditor | undefined): void {
  if (editor && isJsonOrCsv(editor.document)) {
    lastDataEditor = editor;
  }
}

function isJsonOrCsv(document: vscode.TextDocument): boolean {
  const extension = path.extname(document.fileName).toLowerCase();
  return document.languageId === 'json'
    || document.languageId === 'jsonc'
    || extension === '.json'
    || extension === '.csv';
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
