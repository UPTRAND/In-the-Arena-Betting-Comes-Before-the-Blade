---
name: prevent-encoding-corruption
description: Guidelines and scripts to detect and handle different file encodings (e.g., CP949, UTF-8) to prevent text corruption and line break destruction.
---

# Prevent Encoding Corruption

When modifying source code or reading files in projects with mixed encodings (especially projects containing Korean comments in CP949/EUC-KR), follow these strict guidelines to prevent character corruption and line break (CRLF/LF) destruction.

## 1. Prioritize Native Editing Tools

**ALWAYS** prioritize using the provided native editing tools (`multi_replace_file_content`, `replace_file_content`, `write_to_file`) over running custom terminal commands (like `sed`, `awk`, or raw Python scripts via `run_command`).
*   Native tools are built to handle basic encoding preservation and line ending normalization much more safely than arbitrary shell commands.
*   Avoid using `echo`, `cat`, or output redirection (`>`) in PowerShell, as PowerShell often defaults to UTF-16 LE, which will immediately corrupt standard C# files.

## 2. Reading/Writing Files via Custom Scripts (If Necessary)

If you **must** use a Python script via `run_command` to process a file (e.g., for complex regex processing), you must explicitly handle the encoding and line endings.

### A. Detecting & Reading Safely
Do not blindly use `open(filepath, 'r')`. Windows defaults to CP949, but the file might be UTF-8, or vice versa.

```python
def read_safe(filepath):
    encodings = ['utf-8', 'cp949', 'euc-kr']
    for enc in encodings:
        try:
            with open(filepath, 'r', encoding=enc) as f:
                content = f.read()
                return content, enc
        except UnicodeDecodeError:
            continue
    raise ValueError(f"Cannot decode {filepath}")
```

### B. Writing Back Safely
When writing the modified content back to the file, **must use the exact same encoding** detected during the read phase, and preserve line endings.

```python
# newline='' prevents Python from automatically altering original line endings (CRLF -> LF)
with open(filepath, 'w', encoding=original_encoding, newline='') as f:
    f.write(modified_text)
```

## 3. Handling Git Diffs with Mixed Encodings

Git natively outputs diffs in UTF-8. If you generate a diff for a file encoded in CP949, any Korean text in the diff will appear broken if read as UTF-8, or the patch itself may become corrupted when applied.

*   **Reading Diffs**: If you need to read a diff that contains CP949 characters, decode it explicitly rather than relying on default terminal output:
    ```powershell
    python -c "import subprocess; out = subprocess.check_output(['git', 'diff', 'HEAD']); print(out.decode('cp949', errors='replace'))"
    ```
*   **Applying Patches**: If applying a patch fails due to encoding or line ending mismatches, use `git apply --ignore-whitespace` or manually port the changes using the native `multi_replace_file_content` tool based on the patch contents.

## Summary Checklist
- [ ] Is this file likely to contain Korean comments? (Assume CP949 or UTF-8).
- [ ] Am I using the native `multi_replace_file_content` tool instead of risky shell commands?
- [ ] If using Python to write, did I specify the `encoding=` and `newline=''` arguments?
