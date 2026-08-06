---
name: generate-git-patch
description: Generates a git diff patch file for uncommitted changes using the git diff --output command.
---

# Generate Git Patch

When the user requests to generate a diff patch for the current uncommitted changes, follow this exact procedure.

## Execution Steps

Use the `run_command` tool to execute the `git diff` command with the `--output` flag.

```powershell
git diff HEAD --output="<absolute_path_to_save_patch>.patch"
```

*   **`HEAD`**: Compares the working directory against the last commit.
*   **`--output`**: Native Git flag to write the diff directly to a file. This is highly preferred over PowerShell redirection (`>`), which can mangle encodings (e.g., saving as UTF-16 LE).
*   **`<absolute_path_to_save_patch>`**: Replace this with the target path (e.g., `C:\Users\pgu51\Downloads\my_changes.patch`).

## ⚠️ Important Considerations & Known Issues

1.  **Encoding Mismatches (e.g., CP949 vs UTF-8)**
    *   Git generates diffs in **UTF-8** by default. 
    *   If the target files in the Unity project are encoded in **CP949 (euc-kr)** or other encodings, Korean comments and strings in the patch file will be corrupted.
    *   **Action**: Be aware of this limitation. If the user intends to apply this patch later via scripts or `git apply`, the encoding mismatch might break the file.
2.  **Line Endings (CRLF vs LF)**
    *   Depending on the user's `core.autocrlf` setting, Git might output warnings like: `warning: in the working copy of '...', LF will be replaced by CRLF the next time Git touches it`.
    *   **Action**: This warning is generally safe to ignore for the patch generation itself, but you should acknowledge it if asked.
3.  **Always Verify Current State**
    *   Before generating the patch, ensure you know what changes are actually in the working directory by checking `git status` if necessary, so you don't accidentally include unintended modifications.
