using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ConsoleAppFramework;
using SimilarityCSharp.CLI;
using SimilarityCSharp;


ConsoleApp.Run(args, Commands.FindDuplicates);

static class Commands
{
    /// <summary>
    /// Find duplicate code in C# projects using tree edit distance analysis.
    /// </summary>
    /// <param name="paths">-p, Paths to analyze (files or directories).</param>
    /// <param name="threshold">Similarity threshold (0.0-1.0).</param>
    /// <param name="minLines">Minimum lines of code to analyze.</param>
    /// <param name="maxLines">Maximum lines of code to analyze (skip large methods).</param>
    /// <param name="minTokens">Minimum tokens to analyze.</param>
    /// <param name="print">Print method details in output.</param>
    /// <param name="printAll">Print all members in duplicate groups.</param>
    /// <param name="noSizePenalty">Disable size penalty in similarity calculation.</param>
    /// <param name="extensions">-ext, File extensions to analyze. Use .cs if null</param>
    /// <param name="renameCost">Rename cost for Apted algorithm.</param>
    /// <param name="deleteCost">Delete cost for Apted algorithm.</param>
    /// <param name="insertCost">Insert cost for Apted algorithm.</param>
    /// <param name="includeFilePattern">Regex pattern to include files.</param>
    /// <param name="includeMethodPattern">Regex pattern to include methods by name.</param>
    /// <param name="kindDistanceWeight">Weight factor for kind distance (0.0-1.0).</param>
    /// <param name="output">-o, Output file path to save results (if not specified, prints to console).</param>
    public static void FindDuplicates(
        string[]? paths = null,
        double threshold = 0.87,
        int minLines = 5,
        int maxLines = int.MaxValue,
        int minTokens = 0,
        bool print = false,
        bool printAll = false,
        bool noSizePenalty = false,
        string[]? extensions = null,
        double renameCost = 0.3,
        double deleteCost = 1.0,
        double insertCost = 1.0,
        string? includeFilePattern = null,
        string? includeMethodPattern = null,
        double kindDistanceWeight = 0.5,
        string? output = null)
    {
        // Default values
        paths ??= ["."];
        extensions ??= ["cs"];

        try
        {
            // Find all C# files
            var files = new List<string>();
            foreach (var path in paths)
            {
              
                if (File.Exists(path))
                {
                    if (extensions.Any(ext => path.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase)))
                    {
                        files.Add(path.Replace('\\', '/')); // Normalize path for consistency
                    }
                }
                else if (Directory.Exists(path))
                {
                    foreach (var ext in extensions)
                    {
                        files.AddRange(Directory.GetFiles(path, $"*.{ext}", SearchOption.AllDirectories).Select( f => f.Replace('\\', '/')));
                    }
                }
            }

            // Apply regex filtering
            if (includeFilePattern != null)
            {
                var includeRegexes = new Regex(includeFilePattern);
                files = files.DistinctBy(Path.GetFullPath).Where(file => includeRegexes.IsMatch( Path.GetFullPath(file))).ToList();
            }

            if (files.Count == 0)
            {
                Console.WriteLine("No C# files found after filtering.");
                return;
            }

            Console.WriteLine($"Processing {files.Count} files...");

            // Parse files
            var parser = new Parser();
            var parsedFiles = new ConcurrentBag<ParsedFile>();
            Parallel.ForEach(files, file =>
            {
                try
                {
                    var parsed = parser.ParseFile(file);
                    if (parsed.Methods.Count > 0)
                    {
                        parsedFiles.Add(parsed);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing {file}: {ex.Message}");
                }
            });

            Console.WriteLine($"Parsed {parsedFiles.Count} files with methods.");

            // Configure options
            var tsedOptions = new TsedOptions
            {
                MinLines = minLines,
                MaxLines = maxLines,
                MinTokens = minTokens,
                SizePenalty = !noSizePenalty,
                IncludeMethodPattern = includeMethodPattern != null ? new Regex(includeMethodPattern) : null,
                AptedOptions = new AptedOptions
                {
                    RenameCost = renameCost,
                    DeleteCost = deleteCost,
                    InsertCost = insertCost,
                    KindDistanceWeight = kindDistanceWeight
                }
            };

            // Find duplicates
            var detector = new DuplicateDetector(tsedOptions);
            var duplicates = detector.FindDuplicates(parsedFiles, threshold);

            // Output results
            if (output != null)
            {
                // Redirect console output to file
                using var writer = new StreamWriter(output);
                var originalOut = Console.Out;
                Console.SetOut(writer);
                try
                {
                    OutputFormatter.PrintDuplicates(duplicates, print, printAll);
                }
                finally
                {
                    Console.SetOut(originalOut);
                }

                Console.WriteLine($"Results saved to: {output}");
            }
            else
            {
                OutputFormatter.PrintDuplicates(duplicates, print, printAll);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}