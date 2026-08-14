# Initial LSP Feature Roadmap

The first language-server features should follow the way someone actually writes an Expressif expression, rather than trying to cover the full LSP catalogue immediately.

The repository already has document synchronization and syntax-diagnostic infrastructure. These form the foundation for the first user-facing language features.

## 1. Diagnostics

Finish the end-to-end diagnostic experience first: malformed syntax should produce a precise editor diagnostic with the parser message and the correct source range.

```text
@foo | add(
           ^ expected argument / ')'
```

This validates the complete integration chain:

```text
Expressif.Syntax
    ↓
source positions
    ↓
Expressif.LanguageServer
    ↓
LSP diagnostics
    ↓
editor diagnostics
```

It proves that parser positions, document synchronization, LSP ranges, and editor integration all work together.

## 2. Function completion

Function completion should be the first genuinely interactive language feature.

For example:

```text
@foo | text-to-
               ↓
           text-to-lower
           text-to-upper
           text-to-title
           ...
```

Initially, completion does not require sophisticated type inference. When the grammar expects a function or expression, the language server can propose the known Expressif functions.

This provides significant user value while requiring relatively little semantic complexity.

## 3. Function hover

Once function names can be identified, hovering over a function should expose concise documentation.

For example:

```text
text-to-pad-right(length, character)

Pads the input text on the right until the requested length.
```

Hover information should include, where available:

- the function name;
- its signature;
- a short description.

This is especially useful in Expressif because users should not need to remember every function and argument.

## 4. Signature help

Signature help should build on the same function metadata used by completion and hover.

For example:

```text
text-to-pad-right(@myCount, *)
                  ↑
```

with the editor displaying:

```text
text-to-pad-right(length, character)
                  ^^^^^^
```

The language server should identify the active function call and active parameter without duplicating function metadata in the handler itself.

## 5. Semantic highlighting

Semantic tokens should follow completion, hover, and signature help.

Expressif has several useful semantic categories that could be highlighted distinctly:

```text
@variable
function(...)
.field
$n
literals
operators: |  |>
```

Basic syntax highlighting can already be provided by an editor grammar. Semantic highlighting becomes more valuable once the server can distinguish constructs based on their meaning rather than only their lexical form.

## Features to defer

The first iterations should not prioritize:

- go to definition;
- find references;
- rename;
- workspace symbols.

Expressif currently has relatively little notion of declarations across documents. These capabilities would require a broader semantic and workspace model while providing less immediate value than completion, hover, or signature help.

## Shared function metadata

Completion, hover, and signature help should not maintain separate knowledge about Expressif functions. They should reuse a common server-side abstraction that exposes function metadata.

Conceptually:

```text
Expressif.Syntax
      │
      ▼
parsed syntax / node at position
      │
      ▼
Expressif.LanguageServer
      │
      ├── FunctionCatalog
      │      ├── names
      │      ├── parameters
      │      ├── descriptions
      │      └── categories
      │
      ├── CompletionHandler
      ├── HoverHandler
      └── SignatureHelpHandler
```

The exact implementation does not need to be named `FunctionCatalog`, but the language server should have a single source of function metadata that can be projected through multiple LSP capabilities.

## Proposed implementation order

The initial roadmap is:

1. Syntax diagnostics
2. Function completion
3. Function hover
4. Signature help
5. Semantic tokens

Once the VS Code thin client can communicate successfully with the language server, **function completion should be the first new interactive feature**. It provides the largest visible improvement for relatively little semantic complexity and immediately demonstrates the value of the LSP.