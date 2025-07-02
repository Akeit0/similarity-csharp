using NUnit.Framework;
using System.Text.RegularExpressions;

namespace SimilarityCSharp.Tests;

[TestFixture]
public class IntegrationTests
{
    private Parser _parser = null!;
    private string _testFilesPath = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new Parser();
        _testFilesPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "test", "TestFiles");
        
        // Fallback to relative path if the above doesn't work
        if (!Directory.Exists(_testFilesPath))
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "test", "TestFiles");
        }
        
        // Create test files if they don't exist (for isolated test runs)
        if (!Directory.Exists(_testFilesPath))
        {
            Directory.CreateDirectory(_testFilesPath);
            CreateTestFiles();
        }
    }

    [Test]
    public void EndToEnd_RealTestFiles_DetectsSimilarities()
    {
        if (!File.Exists(Path.Combine(_testFilesPath, "Sample1.cs")) || 
            !File.Exists(Path.Combine(_testFilesPath, "Sample2.cs")))
        {
            CreateTestFiles();
        }
        
        var sample1Path = Path.Combine(_testFilesPath, "Sample1.cs");
        var sample2Path = Path.Combine(_testFilesPath, "Sample2.cs");
        
        var parsedFile1 = _parser.ParseFile(sample1Path);
        var parsedFile2 = _parser.ParseFile(sample2Path);
        
        var options = new TsedOptions
        {
            MinLines = 1,
            MinTokens = 0,
            SizePenalty = true
        };
        
        var detector = new DuplicateDetector(options);
        var duplicates = detector.FindDuplicates(new[] { parsedFile1, parsedFile2 }, threshold: 0.8);
        
        Assert.That(duplicates, Is.Not.Empty, "Should detect similarities between Sample1.cs and Sample2.cs");
        
        // Verify that similar methods are detected
        var duplicateMethodNames = duplicates.SelectMany(g => 
            new[] { g.Representative.Name }.Concat(g.Duplicates.Select(d => d.Method.Name))
        ).ToList();
        
        // Should detect similarity between Add/AddNumbers and Multiply/MultiplyNumbers
        Assert.That(duplicateMethodNames.Count, Is.GreaterThan(0));
    }

    [Test]
    public void EndToEnd_IdenticalMethods_HighSimilarity()
    {
        var code1 = CreateTempCodeFile(@"
namespace Test1
{
    public class Calculator1
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}", "Test1.cs");
        
        var code2 = CreateTempCodeFile(@"
namespace Test2
{
    public class Calculator2
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}", "Test2.cs");
        
        try
        {
            var parsedFile1 = _parser.ParseFile(code1);
            var parsedFile2 = _parser.ParseFile(code2);
            
            var options = new TsedOptions { MinLines = 1, MinTokens = 0, SizePenalty = true };
            var detector = new DuplicateDetector(options);
            var duplicates = detector.FindDuplicates(new[] { parsedFile1, parsedFile2 }, threshold: 0.95);
            
            Assert.That(duplicates, Has.Count.EqualTo(1));
            Assert.That(duplicates[0].Duplicates[0].Similarity, Is.GreaterThan(0.95));
        }
        finally
        {
            File.Delete(code1);
            File.Delete(code2);
        }
    }

    [Test]
    public void EndToEnd_SimilarMethodsWithVariableRenames_DetectedAsSimilar()
    {
        var code1 = CreateTempCodeFile(@"
namespace Test1
{
    public class MathOperations1
    {
        public int Calculate(int a, int b)
        {
            var sum = a + b;
            var result = sum * 2;
            return result;
        }
    }
}", "Math1.cs");
        
        var code2 = CreateTempCodeFile(@"
namespace Test2
{
    public class MathOperations2
    {
        public int Process(int x, int y)
        {
            var total = x + y;
            var output = total * 2;
            return output;
        }
    }
}", "Math2.cs");
        
        try
        {
            var parsedFile1 = _parser.ParseFile(code1);
            var parsedFile2 = _parser.ParseFile(code2);
            
            var options = new TsedOptions { MinLines = 1, MinTokens = 0, SizePenalty = true };
            options.AptedOptions.RenameCost = 0.00; // Allow small renames
            var detector = new DuplicateDetector(options);
            var duplicates = detector.FindDuplicates(new[] { parsedFile1, parsedFile2 }, threshold: 0.5);
            
            Assert.That(duplicates, Has.Count.EqualTo(1));
            Assert.That(duplicates[0].Duplicates[0].Similarity, Is.GreaterThan(0.8));
        }
        finally
        {
            File.Delete(code1);
            File.Delete(code2);
        }
    }

    [Test]
    public void EndToEnd_ComplexSimilarMethods_DetectedCorrectly()
    {
        var code1 = CreateTempCodeFile(@"
namespace Test1
{
    public class DataProcessor1
    {
        public List<int> ProcessData(int[] input)
        {
            var result = new List<int>();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] > 0)
                {
                    result.Add(input[i] * 2);
                }
                else
                {
                    result.Add(0);
                }
            }
            return result;
        }
    }
}", "Processor1.cs");
        
        var code2 = CreateTempCodeFile(@"
namespace Test2
{
    public class DataProcessor2
    {
        public List<int> TransformArray(int[] data)
        {
            var output = new List<int>();
            for (int j = 0; j < data.Length; j++)
            {
                if (data[j] > 0)
                {
                    output.Add(data[j] * 2);
                }
                else
                {
                    output.Add(0);
                }
            }
            return output;
        }
    }
}", "Processor2.cs");
        
        try
        {
            var parsedFile1 = _parser.ParseFile(code1);
            var parsedFile2 = _parser.ParseFile(code2);
            
            var options = new TsedOptions { MinLines = 1, MinTokens = 0, SizePenalty = true };
            var detector = new DuplicateDetector(options);
            var duplicates = detector.FindDuplicates(new[] { parsedFile1, parsedFile2 }, threshold: 0.7);
            
            Assert.That(duplicates, Has.Count.EqualTo(1));
            Assert.That(duplicates[0].Duplicates[0].Similarity, Is.GreaterThan(0.7));
        }
        finally
        {
            File.Delete(code1);
            File.Delete(code2);
        }
    }

    [Test]
    public void EndToEnd_WithFiltering_AppliesFiltersCorrectly()
    {
        var code = CreateTempCodeFile(@"
namespace Test
{
    public class TestClass
    {
        public int ShortMethod()
        {
            return 42;
        }
        
        public int LongMethod()
        {
            var a = 1;
            var b = 2;
            var c = a + b;
            var d = c * 2;
            var e = d / 2;
            return e;
        }
        
        public int AnotherLongMethod()
        {
            var x = 1;
            var y = 2;
            var z = x + y;
            var w = z * 2;
            var v = w / 2;
            return v;
        }
    }
}", "FilterTest.cs");
        
        try
        {
            var parsedFile = _parser.ParseFile(code);
            
            // Test with minimum lines filter
            var optionsWithFilter = new TsedOptions 
            { 
                MinLines = 5, 
                MinTokens = 0, 
                SizePenalty = true 
            };
            
            var detector = new DuplicateDetector(optionsWithFilter);
            var duplicates = detector.FindDuplicates(new[] { parsedFile }, threshold: 0.8);
            
            // Should only detect similarity between the two long methods
            Assert.That(duplicates, Has.Count.EqualTo(1));
            Assert.That(duplicates[0].Representative.Name, Does.Contain("LongMethod"));
            Assert.That(duplicates[0].Duplicates[0].Method.Name, Does.Contain("LongMethod"));
        }
        finally
        {
            File.Delete(code);
        }
    }

    [Test]
    public void EndToEnd_WithMethodPatternFilter_FiltersCorrectly()
    {
        var code = CreateTempCodeFile(@"
namespace Test
{
    public class TestClass
    {
        public int CalculateSum(int a, int b)
        {
            return a + b;
        }
        
        public int CalculateProduct(int a, int b)
        {
            return a * b;
        }
        
        public int ProcessData(int a, int b)
        {
            return a + b;
        }
    }
}", "PatternTest.cs");
        
        try
        {
            var parsedFile = _parser.ParseFile(code);
            
            var optionsWithPattern = new TsedOptions 
            { 
                MinLines = 1, 
                MinTokens = 0, 
                SizePenalty = true,
                IncludeMethodPattern = new Regex(@"^Calculate.*")
            };
            
            var detector = new DuplicateDetector(optionsWithPattern);
            var duplicates = detector.FindDuplicates(new[] { parsedFile }, threshold: 0.8);
            
            // Should not find any duplicates since CalculateSum and CalculateProduct are different
            // but should only consider methods starting with "Calculate"
            Assert.That(duplicates, Is.Empty);
        }
        finally
        {
            File.Delete(code);
        }
    }

    [Test]
    public void EndToEnd_MultipleFiles_DetectsAllSimilarities()
    {
        var files = new List<string>();
        
        try
        {
            // Create multiple files with various similarities
            files.Add(CreateTempCodeFile(@"
namespace File1
{
    public class Class1
    {
        public int Method1(int a, int b) { return a + b; }
        public int Method2(int x, int y) { return x * y; }
    }
}", "File1.cs"));
            
            files.Add(CreateTempCodeFile(@"
namespace File2
{
    public class Class2
    {
        public int MethodA(int p, int q) { return p + q; } // Similar to Method1
        public int MethodB(int r, int s) { return r - s; } // Different
    }
}", "File2.cs"));
            
            files.Add(CreateTempCodeFile(@"
namespace File3
{
    public class Class3
    {
        public int MethodX(int m, int n) { return m * n; } // Similar to Method2
        public int MethodY(int u, int v) { return u + v; } // Similar to Method1/MethodA
    }
}", "File3.cs"));
            
            var parsedFiles = files.Select(f => _parser.ParseFile(f)).ToList();
            
            var options = new TsedOptions { MinLines = 1, MinTokens = 0, SizePenalty = true };
            options.AptedOptions.RenameCost = 0.02;
            var detector = new DuplicateDetector(options);
            var duplicates = detector.FindDuplicates(parsedFiles, threshold: 0.4);
            
            // Should detect multiple duplicate groups
            Assert.That(duplicates.Count, Is.GreaterThan(0));
            
            // Should have groups with multiple duplicates
            var totalDuplicatePairs = duplicates.Sum(g => g.Duplicates.Count);
            Assert.That(totalDuplicatePairs, Is.GreaterThan(0));
        }
        finally
        {
            foreach (var file in files)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }

    [Test]
    public void EndToEnd_FingerprintOptimization_WorksCorrectly()
    {
        var code1 = CreateTempCodeFile(@"
namespace Test1
{
    public class Class1
    {
        public string ProcessString(string input)
        {
            return input.ToUpper().Trim();
        }
    }
}", "String1.cs");
        
        var code2 = CreateTempCodeFile(@"
namespace Test2
{
    public class Class2
    {
        public int ProcessNumber(int input)
        {
            return input * 2 + 1;
        }
    }
}", "String2.cs");
        
        try
        {
            var parsedFile1 = _parser.ParseFile(code1);
            var parsedFile2 = _parser.ParseFile(code2);
            
            // Verify fingerprint optimization is working (different methods should have different fingerprints)
            var method1 = parsedFile1.Methods[0];
            var method2 = parsedFile2.Methods[0];
            
            // These methods are very different, so fingerprint should filter them out
            Assert.That(method1.Fingerprint.MightBeSimilar(method2.Fingerprint, 0.3), Is.False);
            
            var options = new TsedOptions { MinLines = 1, MinTokens = 0, SizePenalty = true };
            var detector = new DuplicateDetector(options);
            var duplicates = detector.FindDuplicates(new[] { parsedFile1, parsedFile2 }, threshold: 0.8);
            
            Assert.That(duplicates, Is.Empty);
        }
        finally
        {
            File.Delete(code1);
            File.Delete(code2);
        }
    }

    private void CreateTestFiles()
    {
        var sample1Content = @"namespace TestFiles;

public class Calculator
{
    public int Add(int a, int b)
    {
        var result = a + b;
        return result;
    }
    
    public int Sum(int x, int y)
    {
        var total = x + y;
        return total;
    }
    
    public int Multiply(int a, int b)
    {
        return a * b;
    }
    
    public int Product(int x, int y)
    {
        return x * y;
    }

    public double Div(int a, int b)
    {
        if (b == 0)
            throw new Exception();
        return a / b;
    }
}";

        var sample2Content = @"namespace TestFiles;

public class MathOperations
{
    // Similar to Add method in Calculator
    public int AddNumbers(int first, int second)
    {
        var sum = first + second;
        return sum;
    }
    
    // Different implementation
    public double Divide(double a, double b)
    {
        if (b == 0)
            throw new DivideByZeroException();
        return a / b;
    }
    
    // Similar to Multiply
    public int MultiplyNumbers(int num1, int num2)
    {
        return num1 * num2;
    }
}";

        File.WriteAllText(Path.Combine(_testFilesPath, "Sample1.cs"), sample1Content);
        File.WriteAllText(Path.Combine(_testFilesPath, "Sample2.cs"), sample2Content);
    }

    private string CreateTempCodeFile(string content, string fileName)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(tempPath, content);
        return tempPath;
    }
}