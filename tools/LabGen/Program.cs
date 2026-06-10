using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var manifestPath = Path.Combine(repositoryRoot, "docs", "manifest.json");

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"Missing manifest: {Path.GetRelativePath(repositoryRoot, manifestPath)}");
    return 1;
}

var manifest = await LoadManifestAsync(manifestPath);
var command = args.FirstOrDefault() ?? "generate";

return command switch
{
    "list" => ListLabs(manifest),
    "generate" => GenerateLabs(repositoryRoot, manifest, args.Skip(1).ToArray()),
    "workshop" => GenerateWorkshop(repositoryRoot, manifest, args.Skip(1).ToArray()),
    _ => UnknownCommand(command),
};

static int ListLabs(LabManifest manifest)
{
    foreach (var lab in manifest.Labs)
    {
        Console.WriteLine($"{lab.Id}: {lab.Markdown}");
    }

    return 0;
}

static int GenerateLabs(string repositoryRoot, LabManifest manifest, string[] args)
{
    var requestedLabId = ReadOption(args, "--lab");

    if (ReadOption(args, "--variant") is not null)
    {
        Console.Error.WriteLine("--variant is no longer supported. LabGen now generates one starter snapshot per lab.");
        return 1;
    }

    var labs = requestedLabId is null
        ? manifest.Labs
        : manifest.Labs.Where(lab => lab.Id == requestedLabId).ToArray();

    if (labs.Length == 0)
    {
        Console.Error.WriteLine($"Unknown lab: {requestedLabId}");
        return 1;
    }

    if (requestedLabId is null)
    {
        var outputRoot = Path.Combine(repositoryRoot, manifest.OutputRoot);
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    foreach (var lab in labs)
    {
        GenerateLab(repositoryRoot, manifest, lab);
    }

    return 0;
}

static void GenerateLab(string repositoryRoot, LabManifest manifest, LabDefinition lab)
{
    var outputRoot = Path.Combine(repositoryRoot, manifest.OutputRoot, lab.Id);

    if (Directory.Exists(outputRoot))
    {
        Directory.Delete(outputRoot, recursive: true);
    }

    Directory.CreateDirectory(outputRoot);

    var files = EnumerateIncludedFiles(repositoryRoot, manifest).ToArray();
    var labOrder = manifest.Labs.Select((item, index) => new { item.Id, Index = index })
        .ToDictionary(item => item.Id, item => item.Index, StringComparer.OrdinalIgnoreCase);

    foreach (var sourcePath in files)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, sourcePath);
        var destinationPath = Path.Combine(outputRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (IsTextFile(sourcePath))
        {
            var source = File.ReadAllText(sourcePath);
            var transformed = LabMarkerProcessor.Transform(source, lab.Id, "starter", labOrder);
            File.WriteAllText(destinationPath, transformed);
        }
        else
        {
            File.Copy(sourcePath, destinationPath);
        }
    }

    Console.WriteLine($"Generated {Path.GetRelativePath(repositoryRoot, outputRoot)}");
}

static int GenerateWorkshop(string repositoryRoot, LabManifest manifest, string[] args)
{
    if (args.Length > 0)
    {
        Console.Error.WriteLine("The workshop command does not support options.");
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  dotnet run --project tools/LabGen -- workshop");
        return 1;
    }

    var outputPath = Path.Combine(repositoryRoot, "docs", "workshop.md");

    var missingMarkdown = manifest.Labs
        .Where(lab => string.IsNullOrWhiteSpace(lab.Markdown))
        .ToArray();

    if (missingMarkdown.Length > 0)
    {
        Console.Error.WriteLine("The following labs do not define a markdown file:");
        foreach (var lab in missingMarkdown)
        {
            Console.Error.WriteLine($"  {lab.Id}: {lab.Markdown}");
        }

        return 1;
    }

    var missingFiles = manifest.Labs
        .Select(lab => (Lab: lab, Path: Path.Combine(repositoryRoot, lab.Markdown!)))
        .Where(item => !File.Exists(item.Path))
        .ToArray();

    if (missingFiles.Length > 0)
    {
        Console.Error.WriteLine("The following markdown files are missing:");
        foreach (var item in missingFiles)
        {
            Console.Error.WriteLine($"  {item.Lab.Id}: {Path.GetRelativePath(repositoryRoot, item.Path)}");
        }

        return 1;
    }

    var builder = new StringBuilder();

    foreach (var lab in manifest.Labs)
    {
        var markdownPath = Path.Combine(repositoryRoot, lab.Markdown!);
        var content = File.ReadAllText(markdownPath).Trim();

        if (content.Length == 0)
        {
            continue;
        }

        builder.AppendLine(content);
        builder.AppendLine();
    }

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, builder.ToString());
    Console.WriteLine($"Generated {Path.GetRelativePath(repositoryRoot, outputPath)}");

    return 0;
}

static IEnumerable<string> EnumerateIncludedFiles(string repositoryRoot, LabManifest manifest)
{
    var allFiles = Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
        .Where(path => !IsUnderDirectory(path, Path.Combine(repositoryRoot, ".git")))
        .Where(path => !IsUnderDirectory(path, Path.Combine(repositoryRoot, manifest.OutputRoot)))
        .Where(path => manifest.Exclude.All(pattern => !GlobMatcher.IsMatch(Path.GetRelativePath(repositoryRoot, path), pattern)));

    foreach (var path in allFiles)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, path);
        if (manifest.Include.Any(pattern => GlobMatcher.IsMatch(relativePath, pattern)))
        {
            yield return path;
        }
    }
}

static bool IsUnderDirectory(string path, string directory)
{
    var relative = Path.GetRelativePath(directory, path);
    return relative != "." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && relative != "..";
}

static bool IsTextFile(string path)
{
    var extension = Path.GetExtension(path).ToLowerInvariant();
    return extension is ".cs" or ".csproj" or ".slnx" or ".json" or ".md" or ".http" or ".tsp" or ".yaml" or ".yml" or ".tf" or ".xml";
}

static string? ReadOption(string[] args, string name)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (args[index] == name && index + 1 < args.Length)
        {
            return args[index + 1];
        }
    }

    return null;
}

static async Task<LabManifest> LoadManifestAsync(string manifestPath)
{
    await using var stream = File.OpenRead(manifestPath);
    var manifest = await JsonSerializer.DeserializeAsync<LabManifest>(stream, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    });

    return manifest ?? throw new InvalidOperationException("The lab manifest is empty.");
}

static string FindRepositoryRoot(string startPath)
{
    var directory = new DirectoryInfo(startPath);

    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "DocumentAPI.slnx")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Unable to locate repository root from the current directory.");
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project tools/LabGen -- list");
    Console.Error.WriteLine("  dotnet run --project tools/LabGen -- generate [--lab <id>]");
    Console.Error.WriteLine("  dotnet run --project tools/LabGen -- workshop");
    return 1;
}

internal sealed record LabManifest
{
    public string OutputRoot { get; init; } = "generated/labs";

    public string[] Include { get; init; } = [];

    public string[] Exclude { get; init; } = [];

    public LabDefinition[] Labs { get; init; } = [];
}

internal sealed record LabDefinition
{
    public required string Id { get; init; }

    public required string Markdown { get; init; }
}

internal static partial class LabMarkerProcessor
{
    private const string Starter = "starter";

    public static string Transform(string source, string targetLabId, string targetVariant, IReadOnlyDictionary<string, int> labOrder)
    {
        var targetLabIndex = labOrder[targetLabId];
        var lines = SplitLines(source);
        var output = new List<string>(lines.Length);

        TransformRange(lines, 0, lines.Length, output, targetLabIndex, targetVariant, labOrder);

        return string.Concat(output);
    }

    private static void TransformRange(
        IReadOnlyList<LinePart> lines,
        int startIndex,
        int endIndex,
        List<string> output,
        int targetLabIndex,
        string targetVariant,
        IReadOnlyDictionary<string, int> labOrder)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            var line = lines[index];
            var marker = StartMarkerRegex().Match(line.Content);

            if (!marker.Success)
            {
                if (!HiddenPayloadRegex().IsMatch(line.Content))
                {
                    output.Add(line.Original);
                }

                continue;
            }

            var blockLabId = marker.Groups["id"].Value;
            var selectedState = ResolveState(blockLabId, targetLabIndex, targetVariant, labOrder);
            var blockEndIndex = FindBlockEnd(lines, index + 1, endIndex, blockLabId);

            if (selectedState == Starter)
            {
                AddStarterPayload(lines, index + 1, blockEndIndex, output);
            }
            else
            {
                TransformRange(lines, index + 1, blockEndIndex, output, targetLabIndex, targetVariant, labOrder);
            }

            index = blockEndIndex;
        }
    }

    private static void AddStarterPayload(IReadOnlyList<LinePart> lines, int startIndex, int endIndex, List<string> output)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            if (StartMarkerRegex().IsMatch(lines[index].Content))
            {
                index = FindBlockEnd(lines, index + 1, endIndex, "nested");
                continue;
            }

            if (HiddenPayloadRegex().IsMatch(lines[index].Content))
            {
                output.Add(UnwrapLabLine(lines[index]));
            }
        }
    }

    private static int FindBlockEnd(IReadOnlyList<LinePart> lines, int startIndex, int endIndex, string blockLabId)
    {
        var depth = 0;

        for (var index = startIndex; index < endIndex; index++)
        {
            if (StartMarkerRegex().IsMatch(lines[index].Content))
            {
                depth++;
                continue;
            }

            if (!EndMarkerRegex().IsMatch(lines[index].Content))
            {
                continue;
            }

            if (depth == 0)
            {
                return index;
            }

            depth--;
        }

        throw new InvalidOperationException($"Missing closing lab marker for lab '{blockLabId}'.");
    }

    private static string ResolveState(string blockLabId, int targetLabIndex, string targetVariant, IReadOnlyDictionary<string, int> labOrder)
    {
        if (!labOrder.TryGetValue(blockLabId, out var blockLabIndex))
        {
            return Starter;
        }

        if (blockLabIndex == targetLabIndex)
        {
            return targetVariant;
        }

        return Starter;
    }

    private static string UnwrapLabLine(LinePart line)
    {
        var match = HiddenPayloadRegex().Match(line.Content);
        return match.Success
            ? match.Groups["indent"].Value + match.Groups["content"].Value + line.Ending
            : line.Original;
    }

    private static LinePart[] SplitLines(string source)
    {
        var lines = new List<LinePart>();
        var start = 0;

        while (start < source.Length)
        {
            var newlineIndex = source.IndexOf('\n', start);
            if (newlineIndex < 0)
            {
                var remainingContent = source[start..];
                lines.Add(new LinePart(remainingContent, remainingContent, string.Empty));
                break;
            }

            var lineEnd = newlineIndex;
            var ending = "\n";

            if (lineEnd > start && source[lineEnd - 1] == '\r')
            {
                lineEnd--;
                ending = "\r\n";
            }

            var lineContent = source[start..lineEnd];
            lines.Add(new LinePart(source[start..(newlineIndex + 1)], lineContent, ending));
            start = newlineIndex + 1;
        }

        return lines.ToArray();
    }

    [GeneratedRegex("^\\s*//\\s*<lab\\s+id=\"(?<id>[^\"]+)\"(?:\\s+state=\"starter\")?\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex StartMarkerRegex();

    [GeneratedRegex("^\\s*//\\s*</lab>\\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex EndMarkerRegex();

    [GeneratedRegex("^(?<indent>\\s*)//\\|\\s*(?<content>.*)$")]
    private static partial Regex HiddenPayloadRegex();

    private readonly record struct LinePart(string Original, string Content, string Ending);
}

internal static class GlobMatcher
{
    public static bool IsMatch(string path, string pattern)
    {
        var normalizedPath = Normalize(path);
        var normalizedPattern = Normalize(pattern);
        var regex = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]*") + "$";

        return Regex.IsMatch(normalizedPath, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Normalize(string value)
    {
        return value.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
