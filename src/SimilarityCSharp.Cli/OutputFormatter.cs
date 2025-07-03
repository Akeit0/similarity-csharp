namespace SimilarityCSharp.CLI;

public static class OutputFormatter
{
    public static void PrintDuplicates(List<DuplicateGroup> groups, bool printCode = false, bool printAllMembers = false)
    {
        if (groups.Count == 0)
        {
            Console.WriteLine("No duplicates found.");
            return;
        }
        
        foreach (var group in groups)
        {
            Console.WriteLine($"\nDuplicate Group (Total Impact: {group.TotalImpact:F1} lines):");
            Console.WriteLine(new string('─', 80));
            
            // Print representative
            Console.WriteLine($"  Representative Method:");
            PrintMethodInfo(group.Representative, "    ", isRepresentative: true);
            
            // Print duplicates
            Console.WriteLine($"  Similar Methods ({group.Duplicates.Count}):");
            foreach (var dup in group.Duplicates)
            {
                PrintMethodInfo(dup.Method, "    ");
                Console.WriteLine($"    Similarity: {dup.Similarity:P}, Impact: {dup.Impact:F1} lines");
            }
            
            if (printCode || printAllMembers)
            {
                Console.WriteLine("\n  Code comparison:");
                
                if (printAllMembers)
                {
                    // Print all members in the group - representative + all duplicates
                    Console.WriteLine($"\n  Representative: {group.Representative.FullName}:");
                    PrintMethodCode(group.Representative);
                    
                    foreach (var dup in group.Duplicates)
                    {
                        Console.WriteLine($"\n  Similar: {dup.Method.FullName} (Similarity: {dup.Similarity:P}):");
                        PrintMethodCode(dup.Method);
                    }
                }
                else if (printCode)
                {
                    // Print only representative and top duplicate (original behavior)
                    Console.WriteLine($"  {group.Representative.FullName}:");
                    PrintMethodCode(group.Representative);
                    
                    if (group.Duplicates.Count > 0)
                    {
                        var topDup = group.Duplicates[0];
                        Console.WriteLine($"\n  {topDup.Method.FullName} (Similarity: {topDup.Similarity:P}):");
                        PrintMethodCode(topDup.Method);
                    }
                }
            }
        }
        
        // Summary
        Console.WriteLine(new string('=', 80));
        var totalDuplicates = groups.Sum(g => g.Duplicates.Count + 1);
        var totalImpact = groups.Sum(g => g.TotalImpact);
        Console.WriteLine($"Found {groups.Count} duplicate groups with {totalDuplicates} total methods");
        Console.WriteLine($"Total impact: {totalImpact:F0} duplicate lines");
    }

    static void PrintMethodInfo(MethodInfo method, string indent, bool isRepresentative = false)
    {
        var location = $"{method.FilePath}:{method.StartLine}";
        var info = $"L{method.StartLine}-{method.EndLine} {method.FullName}";
        var marker = isRepresentative ? "★" : " ";
        Console.WriteLine($"{indent}{marker} {location} | {info}");
    }

    static void PrintMethodCode(MethodInfo method)
    {
        try
        {
            var lines = File.ReadAllLines(method.FilePath);
            var startIdx = Math.Max(0, method.StartLine - 1);
            var endIdx = Math.Min(lines.Length, method.EndLine);
            
            for (int i = startIdx; i < endIdx; i++)
            {
                Console.WriteLine($"    {i + 1,4} | {lines[i]}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [Error reading file: {ex.Message}]");
        }
    }
}