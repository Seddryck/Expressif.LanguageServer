# Repository guidelines

## Architecture

`Expressif.LanguageServer` provides Language Server Protocol support for the Expressif language.

The repository MUST preserve a clear separation between:

```text
LSP client
    |
    v
Protocol / handlers
    |
    v
Language-server services
    |
    +--> Expressif.Syntax
    |
    +--> future Expressif binding / semantic services
```

### Protocol boundary

LSP infrastructure is an implementation detail of the server boundary.

Types from `OmniSharp.Extensions.LanguageServer.Protocol` or another LSP framework MAY be used by handlers and protocol adapters, but SHOULD NOT leak into the public API of protocol-independent language-server services.

In particular, core services SHOULD NOT expose protocol types such as:

```text
DocumentUri
Position
Range
Diagnostic
CompletionItem
Hover
Location
```

unless the service is explicitly protocol-specific.

Prefer internal concepts where the distinction is useful, and translate between internal and LSP representations at the protocol boundary.

Do NOT introduce wrappers merely to mirror every LSP type. The goal is architectural separation, not abstraction for its own sake.

### Handlers

LSP entry points MUST be implemented as handlers.

Handlers SHOULD remain thin. Their responsibilities are primarily to:

1. receive an LSP request or notification;
2. translate protocol data when necessary;
3. invoke the appropriate language-server service;
4. translate the result into the LSP response.

Expressif-specific parsing, diagnostics, completion, hover, navigation, binding, or semantic behavior SHOULD NOT be implemented directly in handlers when it can live in a protocol-independent service.

### Syntax

`Expressif.Syntax` is the canonical parser and syntax-tree implementation for Expressif.

The language server MUST NOT:

* implement a second Expressif parser;
* duplicate the Expressif grammar;
* reconstruct syntax by interpreting source text independently when the required information is available from `Expressif.Syntax`.

Syntax-related behavior SHOULD build on the syntax tree, tokens, spans, and diagnostics exposed by `Expressif.Syntax`.

If required syntax information is missing, prefer improving `Expressif.Syntax` rather than introducing a competing representation in the language server.

### Syntax and semantics

Keep syntax analysis separate from semantic or binding analysis.

Conceptually:

```text
source text
    |
    v
Expressif.Syntax
    |
    v
syntax tree
    |
    v
language-server syntax services
    |
    v
future binding / semantic services
```

Function resolution, parameter validation, schemas, providers, types, and other binding concerns SHOULD NOT be embedded in syntax services.

### Core and host responsibilities

Protocol-independent behavior belongs in `Expressif.LanguageServer.Core` whenever practical.

The executable/host project is responsible for concerns such as:

* process startup;
* stdin/stdout transport;
* LSP server configuration;
* handler registration;
* dependency injection;
* protocol adapters.

`Expressif.LanguageServer.Core` SHOULD contain reusable and independently testable concepts such as:

* document state;
* syntax services;
* diagnostic computation;
* completion logic;
* hover logic;
* navigation logic;
* future semantic services.

Do not move protocol dependencies into Core merely for convenience.

### Document state

Open-document state MUST be owned by the language server rather than by individual handlers.

Handlers that process `didOpen`, `didChange`, or `didClose` SHOULD delegate document-state management to the document service/store.

Services that require current source text or parsed syntax SHOULD obtain it through the document abstraction rather than maintaining independent caches.

### Editor independence

Language-server behavior MUST remain editor-independent.

Do NOT add VS Code-, Zed-, Visual Studio-, or other editor-specific behavior to the core language server unless it is required to implement a standardized LSP capability.

Editor integrations should remain thin clients of the language server.

---

## Issues

Issue titles MUST be descriptive natural-language titles.

Do NOT use Conventional Commit syntax for issue titles.

Prefer:

```text
Publish syntax diagnostics for open documents
Add completion for function names
Keep document syntax trees synchronized
```

Avoid:

```text
feat: publish syntax diagnostics
feat(lsp): add function completion
fix: synchronize syntax trees
```

Every issue MUST have exactly one change-type label:

* `bug` for a defect;
* `new-feature` for new functionality;
* `enhancement` for an improvement or refactoring of existing functionality.

The label is determined by the nature of the issue.

---

## Branches and worktrees

Every coding task MUST be performed in its own dedicated worktree and task branch.

For a new task:

1. fetch the latest remote state;
2. create the task branch from the latest `origin/main`;
3. create or use a dedicated worktree for that branch.

Branch names MUST describe the nature of the change:

* `fix/<name>` for bug fixes and incorrect behavior;
* `feat/<name>` for new functionality;
* `refactor/<name>` for internal restructuring without intended behavior changes;
* `perf/<name>` for performance improvements;
* `docs/<name>` for documentation-only changes;
* `test/<name>` for test-only changes;
* `chore/<name>` for maintenance work that does not fit another category.

When asked to fix a bug, defect, regression, or issue describing incorrect behavior, the branch MUST use the `fix/` prefix.

For example:

```text
fix/document-version-tracking
feat/syntax-diagnostics
feat/function-completion
refactor/protocol-boundary
```

Do NOT use tooling-specific prefixes such as:

```text
codex/
chatgpt/
```

The agent performing the task MUST NOT affect the branch name.

Branch names SHOULD be derived from the nature or title of the issue and SHOULD NOT contain the issue number.

---

## Conventional Commits

Commit messages and pull request titles MUST use the following form:

```text
<type>(<scope>): <description>
```

The scope is optional.

When the description starts with a word, that word MUST start with a lowercase letter.

Prefer scopes that identify the affected architectural area when useful, for example:

```text
feat(lsp): handle document synchronization
feat(diagnostics): publish parser diagnostics
feat(completion): complete function names
fix(documents): preserve document version
refactor(core): isolate protocol types
test(syntax): cover malformed pipelines
ci(deps): bump actions/checkout from 6 to 7
```

Avoid:

```text
feat(lsp): Handle document synchronization
fix: Fix document version
```

Use `ci` for CI configuration and scripts, including GitHub Actions workflows and their dependencies.

Use `build` for the build system and external project/build dependencies, including NuGet and npm dependencies.

Only `build`, `feat`, `fix`, and `perf` commits should normally increment the GitVersion-calculated release version. By default, add `+semver: skip` to the commit message body when using `chore`, `refactor`, `revert`, `ci`, or `style` so those commits do not trigger a release version increment.

---

## Dependencies

Dependencies MUST respect the intended architectural direction.

Prefer:

```text
LSP infrastructure
      |
      v
LanguageServer host / handlers
      |
      v
LanguageServer.Core
      |
      v
Expressif.Syntax
```

Future Expressif binding or semantic libraries MAY also be consumed by Core, but MUST remain independent of the LSP implementation.

`Expressif.Syntax` MUST NOT depend on `Expressif.LanguageServer`.

Protocol-independent Expressif libraries MUST NOT take a dependency on OmniSharp or another LSP framework merely to support the language server.

Before adding a dependency, consider whether it belongs:

* to the protocol/host layer;
* to Core;
* or to another Expressif repository.

---

## Testing

Prefer tests at the lowest layer that owns the behavior.

Protocol-independent language behavior SHOULD be tested through Core services without starting an LSP server.

Examples:

```text
DocumentStore behavior        -> Core tests
syntax diagnostic computation -> Core tests
completion computation        -> Core tests
LSP range translation         -> handler/protocol tests
handler dispatch behavior     -> handler/protocol tests
stdio lifecycle               -> integration tests
```

Do NOT test language behavior only through the LSP transport when it can be tested directly.

When fixing a defect, add or update a test that demonstrates the failing behavior whenever practical.

Tests involving source positions or ranges SHOULD explicitly cover boundary-sensitive cases where relevant, such as:

* beginning and end of documents;
* multiline expressions;
* incomplete syntax;
* zero-based LSP line and character positions;
* source spans crossing lines.

---

## LSP behavior

Implement standardized LSP behavior before introducing custom protocol extensions.

When implementing a capability:

1. identify the corresponding LSP request, notification, or capability;
2. keep transport-specific handling in the handler;
3. implement reusable language behavior in Core;
4. use `Expressif.Syntax` for syntax understanding;
5. translate positions, ranges, diagnostics, and other protocol objects only at the boundary;
6. add Core tests and protocol tests at the appropriate levels.

Custom LSP methods SHOULD only be introduced when the required behavior cannot reasonably be represented by the standard protocol.

---

## Skills

Repository-specific workflows are defined under `.github/skills/`.

When a task matches an existing skill, read and follow that skill before making changes.

Skills define task-specific procedures. `AGENTS.md` defines repository-wide rules and takes precedence if a skill contains conflicting Git, worktree, branch, issue, commit, pull-request, testing, dependency, or architectural instructions.

---

## Pull requests

For every completed implementation:

1. push the task branch;
2. create a GitHub pull request targeting `main`;
3. use a Conventional Commit-style PR title;
4. include a concise description of the change;
5. include the relevant tests or validation performed;
6. link the pull request to the corresponding issue when one exists using wording that closes the issue.

Do NOT use `bug`, `new-feature`, or `enhancement` labels on the pull request unless explicitly requested.

Pull requests SHOULD remain focused on one coherent change.

Avoid unrelated cleanup or refactoring unless it is necessary to implement the requested change.

---

## Completion criteria

A coding task is complete only when:

* implementation was performed in the task's dedicated worktree;
* for a new task, the branch was created from the latest `origin/main`;
* the branch name follows the repository branch naming rules;
* architectural boundaries described in this file are preserved;
* protocol-independent logic is kept outside handlers where practical;
* parsing uses `Expressif.Syntax` rather than duplicated parsing logic;
* the solution builds successfully;
* the relevant tests have been run;
* all intended changes are committed;
* commit messages follow Conventional Commits;
* the branch has been pushed;
* a pull request targeting `main` has been created;
* the PR title follows Conventional Commits;
* the corresponding issue has the appropriate `bug`, `new-feature`, or `enhancement` label;
* the pull request is linked to the issue when one exists;
* the worktree is clean.
