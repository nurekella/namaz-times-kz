---
name: release
description: Publish a new version of Namaz Times KZ to all users (version bump, changelog, tag, CI release). Use when the owner asks for a release or says "делай сразу все" after a finished change — and only after his explicit "yes" to push.
---

# Release
A pushed tag `vX.Y.Z` reaches every installed copy through self-update. Treat it as a prod deployment.

1. Bump `<Version>` in `NamazTimes.csproj` (semver: feature → minor, fix → patch).
2. Prepend the new entry as the FIRST element of `changelog.json`: `{"v","d","kk":[...],"ru":[...],"en":[...]}`.
   The selftest fails if the first entry ≠ the csproj version. Validate JSON.
3. Update `README.md` if features or data sources changed (attributions/licences in the licence section).
4. Verify: `dotnet build -c Release` and `dotnet run -c Release --no-build -- --selftest` → `selftest ok`. Show the output.
5. Commit to `main` with the agent's `Co-Authored-By` trailer; `git tag vX.Y.Z`.
6. Ask the owner "push?" → after "yes": `git push origin main vX.Y.Z`.
7. `gh run watch <id> --exit-status`, then `gh release view vX.Y.Z --json assets` — expect `NamazTimes.exe` and `NamazTimes-X.Y.Z.msi`.
8. Report the version and what changed in Russian; the owner updates from inside the app.
