# LabGen

LabGen creates workshop starter snapshots from a single maintained source tree.

The solution for a lab is intentionally the starter snapshot of the next lab. This keeps generated output small and avoids maintaining duplicate solution folders.

## Commands

```bash
dotnet run --project tools/LabGen -- list
dotnet run --project tools/LabGen -- generate
dotnet run --project tools/LabGen -- generate --lab 1
dotnet run --project tools/LabGen -- workshop
```

The `workshop` command concatenates the markdown files declared by `docs/manifest.json` into `docs/workshop.md`.

## Marker format

Use one starter block around the maintained solution code:

```csharp
// <lab id="01-document-upload-persistence">
//|throw new NotImplementedException("Complete this lab step.");
return new CompletedValue();
// </lab>
```

For a generated lab, previous labs keep the maintained solution code inside their lab blocks. The current lab and future labs keep only the `//|` starter payload.
