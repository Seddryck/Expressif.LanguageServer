import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync } from 'node:fs';
import { arch, platform } from 'node:process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const extensionDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repositoryDirectory = path.resolve(extensionDirectory, '..');
const runtime = runtimeIdentifier();
const outputDirectory = path.join(extensionDirectory, 'server', runtime);
const artifactsDirectory = path.join(extensionDirectory, 'artifacts');
const project = path.join(
  repositoryDirectory,
  'src',
  'Expressif.LanguageServer',
  'Expressif.LanguageServer.csproj'
);

rmSync(outputDirectory, { recursive: true, force: true });
mkdirSync(outputDirectory, { recursive: true });
mkdirSync(artifactsDirectory, { recursive: true });

const result = spawnSync('dotnet', [
  'publish', project,
  '--configuration', 'Release',
  '--framework', 'net10.0',
  '--runtime', runtime,
  '--self-contained', 'true',
  '--nologo',
  '-p:TargetFrameworks=net10.0',
  '--output', outputDirectory
], { cwd: repositoryDirectory, stdio: 'inherit' });

if (result.error) {
  throw result.error;
}
if (result.status !== 0) {
  process.exit(result.status ?? 1);
}

function runtimeIdentifier() {
  const runtimeArchitecture = arch === 'arm64' ? 'arm64' : 'x64';
  switch (platform) {
    case 'win32': return `win-${runtimeArchitecture}`;
    case 'linux': return `linux-${runtimeArchitecture}`;
    case 'darwin': return `osx-${runtimeArchitecture}`;
    default: throw new Error(`Unsupported platform: ${platform}/${arch}`);
  }
}
