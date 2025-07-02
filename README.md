# SimilarityCSharp

A high-performance C# code similarity detector using **Tree Structure Edit Distance (TSED)** analysis with Roslyn. Efficiently finds duplicate and similar code across C# projects through advanced AST comparison algorithms.


This repository is highly inspired by [**similarity**](https://github.com/mizchi/similarity).
##  Features

- **Advanced Similarity Detection**: Uses Tree Structure Edit Distance (TSED) with APTED algorithm for precise code similarity analysis
- **High Performance**: Optimized with Bloom filters and parallel processing for large codebases
- **Flexible Configuration**: Extensive options for thresholds, filtering, and algorithm parameters
- **AST-Based Analysis**: Leverages Microsoft Roslyn for accurate C# syntax tree analysis
- **Smart Filtering**: Configurable method size, pattern matching, and file inclusion rules
- **Detailed Reporting**: Comprehensive duplicate detection with similarity scores and impact analysis


### Requirements
- .NET 8.0 or later
- C# projects using Roslyn-compatible syntax

### Build from Source
```bash
git clone https://github.com/Akeit0/similarity-csharp.git
cd similarity-csharp
dotnet build
```

## Quick Start
```
dotnet tool install --global  similarity-csharp
similarity-csharp -h
```

### Basic Usage
```bash
# Analyze current directory for duplicates
similarity-csharp -p . --threshold 0.8

# Analyze specific files/directories
similarity-csharp -p "src/" "tests/" --threshold 0.85 --min-lines 10

# Find high-similarity code with detailed output
similarity-csharp -p . --threshold 0.9 --print --output results.txt
```

### Programmatic Usage
```csharp
using SimilarityCSharp.Core;
using SimilarityCSharp.Detection;
using SimilarityCSharp.Parsing;

// Configure detection options
var options = new TSEDOptions
{
    MinLines = 5,
    MaxLines = 1000,
    MinTokens = 10,
    SizePenalty = true,
    APTEDOptions = new APTEDOptions
    {
        RenameCost = 0.3,
        DeleteCost = 1.0,
        InsertCost = 1.0
    }
};

// Parse source files
var parser = new Parser();
var files = new[]
{
    parser.ParseFile("MyClass1.cs"),
    parser.ParseFile("MyClass2.cs")
};

// Detect duplicates
var detector = new DuplicateDetector(options);
var duplicates = detector.FindDuplicates(files, threshold: 0.85);

// Process results
foreach (var group in duplicates)
{
    Console.WriteLine($"Representative: {group.Representative.FullName}");
    foreach (var duplicate in group.Duplicates)
    {
        Console.WriteLine($"  Similar: {duplicate.Method.FullName} (similarity: {duplicate.Similarity:F2})");
    }
}
```

##  Configuration Options

### Core Parameters
| Parameter | Default | Description |
|-----------|---------|-------------|
| `threshold` | 0.87 | Similarity threshold (0.0-1.0) |
| `min-lines` | 5 | Minimum lines of code to analyze |
| `max-lines` | 2147483647 | Maximum lines of code (skip large methods) |
| `min-tokens` | 0 | Minimum tokens to analyze |
| `no-size-penalty  ` | false | Disable size penalty in similarity calculation |

### APTED Algorithm Parameters
| Parameter | Default | Description |
|-----------|---------|-------------|
| `rename-cost` | 0.3 | Cost of renaming nodes in tree edit distance |
| `delete-cost` | 1.0 | Cost of deleting nodes |
| `insert-cost` | 1.0 | Cost of inserting nodes |
| `kind-distance-weight` | 0.5 | Weight factor for syntax kind differences |

### Filtering Options
| Parameter | Description |
|-----------|-------------|
| `include-file-pattern` | Regex pattern to include specific files |
| `include-method-pattern` | Regex pattern to include methods by name |
| `extensions` | File extensions to analyze (default: .cs) |

## Algorithm Details

### Tree Structure Edit Distance (TSED)
SimilarityCSharp uses an advanced TSED algorithm based on APTED (All Pairs Tree Edit Distance) with optimizations:

1. **AST Generation**: Converts C# code to syntax trees using Roslyn
2. **Fingerprinting**: Uses Bloom filters for fast pre-filtering of dissimilar methods
3. **Edit Distance**: Calculates minimum operations to transform one tree to another
4. **Normalization**: Applies size-based normalization with optional penalties
5. **Similarity Scoring**: Converts edit distance to similarity percentage

### Performance Optimizations
- **Bloom Filter Pre-filtering**: Eliminates obviously dissimilar methods before expensive calculations
- **Parallel Processing**: Multi-threaded similarity calculations for large codebases
- **Memory-Efficient Trees**: Optimized tree representations with caching
- **Early Termination**: Stops calculations when similarity falls below threshold

##  Understanding Results

### Similarity Scores
- **1.0**: Identical code structure
- **0.9-0.99**: Nearly identical (minor variable renames)
- **0.8-0.89**: Very similar (small structural differences)
- **0.7-0.79**: Similar (moderate differences)
- **< 0.7**: Different structures

### Impact Analysis
The tool calculates impact scores based on:
- Total lines of code in similar methods
- Similarity percentage
- Potential refactoring benefit


## Advanced Usage Examples

### Custom Algorithm Configuration
```csharp
var options = new TSEDOptions
{
    MinLines = 3,
    SizePenalty = false,  // Disable size penalty
    APTEDOptions = new APTEDOptions
    {
        RenameCost = 0.1,     // Low cost for renames (more permissive)
        DeleteCost = 0.8,     // Lower cost for deletions
        InsertCost = 0.8      // Lower cost for insertions
    },
    IncludeMethodPattern = new Regex(@"^(Get|Set|Process).*")  // Only analyze specific methods
};
```

### Batch Processing Multiple Projects
```csharp
var parser = new Parser();
var allFiles = new List<ParsedFile>();

// Process multiple directories
foreach (var directory in new[] { "ProjectA/", "ProjectB/", "Common/" })
{
    var csFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
    foreach (var file in csFiles)
    {
        allFiles.Add(parser.ParseFile(file));
    }
}

var detector = new DuplicateDetector(options);
var results = detector.FindDuplicates(allFiles, threshold: 0.8);
```

### Fine-tuning for Different Code Types

#### For Test Code (more permissive)
```csharp
var testOptions = new TSEDOptions
{
    MinLines = 3,
    APTEDOptions = new() { RenameCost = 0.1 },  // Allow easy variable renames
    IncludeMethodPattern = new Regex(@".*Test.*")
};
```

#### For Production Code (strict)
```csharp
var productionOptions = new TSEDOptions
{
    MinLines = 10,
    SizePenalty = true,
    APTEDOptions = new() { RenameCost = 0.5 },  // Stricter on renames
    IncludeMethodPattern = new Regex(@"^(?!.*Test).*")  // Exclude test methods
};
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

##  Acknowledgments

- **Microsoft Roslyn** - C# compiler platform and syntax analysis
- **APTED Algorithm** - Efficient tree edit distance computation
- **ZLinq** - High-performance LINQ operations
- **NUnit** - Testing framework
- [**similarity**](https://github.com/mizchi/similarity)
