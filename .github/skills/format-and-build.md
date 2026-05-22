---
name: format-and-build
description: Pre-commit quality gate — run dotnet format then build the solution. Use this skill before committing any code changes to ensure formatting is clean and the build succeeds.
---

# Format and Build Skill

Run this quality gate **before every commit**. CI/CD will reject PRs that fail formatting or have build errors.

## Step 1: Check Formatting

```bash
cd D:\Personal\eatmyentireass.online
dotnet format --verify-no-changes
```

- **Exit code 0** — formatting is clean, proceed to Step 2
- **Exit code 2** — formatting issues found, run the auto-fix below

### Auto-fix Formatting

```bash
dotnet format
```

Expected time: ~12 seconds. **Do not cancel** — wait for it to complete.

After fixing, re-run `dotnet format --verify-no-changes` to confirm it's clean.

---

## Step 2: Build the Solution

```bash
dotnet build EMEAOnline.slnx
```

Expected time: ~6 seconds (after initial restore). **Do not cancel**.

### Interpreting Output

| Result | Meaning |
|--------|---------|
| `0 Error(s)` | ✅ Build succeeded — safe to commit |
| `N Warning(s)` | ✅ OK — nullable reference warnings are expected |
| `1+ Error(s)` | ❌ Fix errors before committing |

---

## Windows: DLL Locking

If the build fails with `file is being used by another process`:

1. Stop any running `dotnet run` processes (Blazor client)
2. Stop any running `func start` processes (Azure Functions API)
3. Retry the build

---

## Full Pre-Commit Command

Run both in sequence:

```bash
dotnet format && dotnet build EMEAOnline.slnx
```

Only commit if both complete with no errors.

---

## Release Build (for CI verification)

To verify the release configuration (mirrors what CI runs):

```bash
dotnet build EMEAOnline.slnx --configuration Release
```

---

## Note on Existing Extensions

The `build-client` extension (`.github/extensions/build-client/`) only builds — it does not format. Use this skill (format + build together) as the pre-commit gate; use `build-client` for mid-development quick builds.
