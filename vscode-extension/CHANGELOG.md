# Changelog

## Unreleased

- Add persistent compact/pretty evaluation output settings with configurable pretty indentation.
- Highlight deprecated implicit tuple binding and offer safe migrations to `~function` or `function~`.
- Support completion, hover, semantic highlighting, and callable-aware actions for explicit tuple-binding shorthand.
- Prompt for `.expressif` or `.json` output and serialize evaluation results in the selected format.
- Add `Ctrl+Shift+Enter` to run with the most recently selected input and output format.
- Add a Quick Pick for choosing the input source before evaluating an expression, including the active JSON/CSV editor while retaining the most recently active Expressif expression.
- Add a command to run the selected expression or active document, prompting for input only when required.
- Enable VS Code's line-comment and block-comment commands for Expressif files.

## 0.1.0

- Register `.expressif` files and launch the bundled Expressif language server over stdio.
