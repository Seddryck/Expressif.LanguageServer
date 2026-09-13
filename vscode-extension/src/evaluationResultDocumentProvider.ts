import * as path from 'node:path';
import * as vscode from 'vscode';

export const evaluationResultScheme = 'expressif-evaluation';

export type EvaluationResultFormat = 'Expressif' | 'Json';

export class EvaluationResultDocumentProvider implements vscode.TextDocumentContentProvider, vscode.Disposable {
  private readonly contents = new Map<string, string>();
  private readonly contentChanged = new vscode.EventEmitter<vscode.Uri>();

  readonly onDidChange = this.contentChanged.event;

  provideTextDocumentContent(uri: vscode.Uri): string {
    return this.contents.get(uri.toString()) ?? '';
  }

  update(sourceUri: vscode.Uri, format: EvaluationResultFormat, content: string): vscode.Uri {
    const resultUri = createEvaluationResultUri(sourceUri, format);
    this.contents.set(resultUri.toString(), content);
    this.contentChanged.fire(resultUri);
    return resultUri;
  }

  dispose(): void {
    this.contentChanged.dispose();
    this.contents.clear();
  }
}

function createEvaluationResultUri(
  sourceUri: vscode.Uri,
  format: EvaluationResultFormat
): vscode.Uri {
  const sourceName = path.posix.basename(sourceUri.path) || 'untitled';
  const sourceStem = sourceName.replace(/\.[^.]*$/, '') || 'result';
  const extension = format === 'Json' ? 'json' : 'expressif';

  return vscode.Uri.from({
    scheme: evaluationResultScheme,
    authority: 'result',
    path: `/${sourceStem}.result.${extension}`,
    query: `source=${encodeURIComponent(sourceUri.toString(true))}`
  });
}
