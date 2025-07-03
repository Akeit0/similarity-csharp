using System.Collections.Concurrent;
using ZLinq;

namespace SimilarityCSharp;

public class DuplicateDetector(TsedOptions options)
{
    record struct ComparisonWork(MethodInfo Method1, MethodInfo Method2, int Method1Index, int Method2Index);

    record struct SimilarityResult(MethodInfo Method1, MethodInfo Method2, int Method1Index, int Method2Index, double Similarity);

    public List<DuplicateGroup> FindDuplicates(IEnumerable<ParsedFile> files, double threshold = 0.85)
    {
        var allMethods = files.SelectMany(f => f.Methods).Where(m => 
            m.Lines >= options.MinLines &&
            (m.Lines <= options.MaxLines) &&
            ( m.Tokens >= options.MinTokens &&(options.IncludeMethodPattern == null || options.IncludeMethodPattern.IsMatch(m.FullName)))
        ).ToArray();
        
        // Create all comparison work items upfront
        var comparisonWork = new List<ComparisonWork>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"Starting duplicate detection with {allMethods.Length} methods...");
        for (int i = 0; i < allMethods.Length; i++)
        {
            for (int j = i + 1; j < allMethods.Length; j++)
            {
                var method1 = allMethods[i];
                var method2 = allMethods[j];
                //if(method1.FullName == method2.FullName) continue;
                // Quick fingerprint check
                if (!method1.Fingerprint.MightBeSimilar(method2.Fingerprint, threshold * 0.5))
                    continue;
                
                comparisonWork.Add(new ComparisonWork(method1, method2, i, j));
            }
        }
        Console.WriteLine($"Total comparison work items created: {comparisonWork.Count}");
        
        // Process all similarity calculations in parallel
        var similarityResults = new ConcurrentBag<SimilarityResult>();
        
        Parallel.ForEach(comparisonWork,work =>
        {
            // Calculate similarity - all similarity calculations done in parallel
            var similarity = TsedCalculator.CalculateSimilarity(work.Method1, work.Method2, options);
            
            if (similarity >= threshold)
            {
                similarityResults.Add(new (work.Method1, work.Method2, work.Method1Index, work.Method2Index, similarity));
            }
        });
        
        stopwatch.Stop();
        Console.WriteLine($"Similarity calculations completed in {stopwatch.ElapsedMilliseconds} ms. Found {similarityResults.Count} similar method pairs.");
        // Build duplicate groups from results
        var duplicateGroups = new List<DuplicateGroup>();
        var processed = new HashSet<MethodInfo>();
        
        // Group results by Method1 (representative)
        var groupedResults = similarityResults.AsValueEnumerable()
            .GroupBy(r => r.Method1Index)
            .OrderBy(g => g.Key);
        
        foreach (var group in groupedResults)
        {
            var method1 = group.First().Method1;
            
            if (processed.Contains(method1)) continue;
            
            var duplicateGroup = new DuplicateGroup { Representative = method1 };
            
            // Process results in original comparison order
            using   var orderedResults = group.AsValueEnumerable().OrderBy(r => r.Method2Index).ToArrayPool();
            
            foreach (var result in orderedResults.Span)
            {
                if (!processed.Contains(result.Method2))
                {
                    duplicateGroup.Duplicates.Add(new DuplicateInfo
                    {
                        Method = result.Method2,
                        Similarity = result.Similarity,
                        Impact = CalculateImpact(result.Method1, result.Method2, result.Similarity)
                    });
                    processed.Add(result.Method2);
                }
            }
            
            if (duplicateGroup.Duplicates.Count > 0)
            {
                duplicateGroups.Add(duplicateGroup);
                processed.Add(method1);
            }
        }
        
        // Sort by impact
        foreach (var group in duplicateGroups)
        {
            group.Duplicates.Sort((a, b) => b.Impact.CompareTo(a.Impact));
        }
        
        duplicateGroups.Sort((a, b) => b.TotalImpact.CompareTo(a.TotalImpact));
        
        return duplicateGroups;
    }

    double CalculateImpact(MethodInfo method1, MethodInfo method2, double similarity)
    {
        var totalLines = method1.Lines + method2.Lines;
        return totalLines * similarity;
    }
}

public class DuplicateGroup
{
    public required MethodInfo Representative { get; init; }
    public List<DuplicateInfo> Duplicates { get; } = new();
    
    public double TotalImpact => Duplicates.Sum(d => d.Impact);
}

public class DuplicateInfo
{
    public required MethodInfo Method { get; init; }
    public required double Similarity { get; init; }
    public required double Impact { get; init; }
}