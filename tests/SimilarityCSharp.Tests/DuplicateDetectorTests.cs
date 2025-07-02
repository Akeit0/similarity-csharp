using NUnit.Framework;
using System.Text.RegularExpressions;

namespace SimilarityCSharp.Tests;

[TestFixture]
public class DuplicateDetectorTests
{
    private Parser _parser = null!;
    private TsedOptions _defaultOptions = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new Parser();
        _defaultOptions = new TsedOptions
        {
            MinLines = 1,
            MinTokens = 0,
            SizePenalty = true
        };
    }

    [Test]
    public void FindDuplicates_WithIdenticalMethods_ReturnsDuplicateGroup()
    {
        var code1 = @"
public int Add1(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public int Add2(int a, int b)
{
    return a + b;
}";

        var files = CreateParsedFiles(new[] { code1, code2 });
        var detector = new DuplicateDetector(_defaultOptions);
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.95);
        
        Assert.That(duplicates, Has.Count.EqualTo(1));
        Assert.That(duplicates[0].Duplicates, Has.Count.EqualTo(1));
        Assert.That(duplicates[0].Duplicates[0].Similarity, Is.GreaterThan(0.95));
    }

    [Test]
    public void FindDuplicates_WithSimilarMethods_ReturnsDuplicateGroup()
    {
        var code1 = @"
public int Add(int a, int b)
{
    var result = a + b;
    return result;
}";
        
        var code2 = @"
public int Sum(int x, int y)
{
    var total = x + y;
    return total;
}";

        var files = CreateParsedFiles(new[] { code1, code2 });
        var detector = new DuplicateDetector(_defaultOptions with{ AptedOptions = _defaultOptions.AptedOptions with { RenameCost = 0} });
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.8);
        
        Assert.That(duplicates, Has.Count.EqualTo(1));
        Assert.That(duplicates[0].Duplicates[0].Similarity, Is.GreaterThan(0.8));
    }

    [Test]
    public void FindDuplicates_WithDifferentMethods_ReturnsNoDuplicates()
    {
        var code1 = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public string GetName()
{
    return ""test"";
}";

        var files = CreateParsedFiles(new[] { code1, code2 });
        var detector = new DuplicateDetector(_defaultOptions);
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.8);
        
        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void FindDuplicates_WithMinLinesFilter_FiltersShortMethods()
    {
        var options = new TsedOptions
        {
            MinLines = 5,
            MinTokens = 0,
            SizePenalty = true
        };

        var shortCode = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var longCode = @"
public int Calculate(int a, int b)
{
    var temp1 = a;
    var temp2 = b;
    var result = temp1 + temp2;
    return result;
}";

        var files = CreateParsedFiles(new[] { shortCode, longCode });
        var detector = new DuplicateDetector(options);
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.8);
        
        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void FindDuplicates_WithMethodPattern_FiltersMethodsByName()
    {
        var options = new TsedOptions
        {
            MinLines = 1,
            MinTokens = 0,
            SizePenalty = true,
            IncludeMethodPattern = new Regex(@"^Add.*")
        };

        var addCode = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var addCodeCopy = @"
public int AddNumbers(int a, int b)
{
    return a + b;
}";
        
        var multiplyCode = @"
public int Multiply(int a, int b)
{
    return a * b;
}";

        var files = CreateParsedFiles(new[] { addCode, addCodeCopy, multiplyCode });
        var detector = new DuplicateDetector(options);
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.1);
        
        Assert.That(duplicates, Has.Count.EqualTo(1));
        Assert.That(duplicates[0].Representative.Name, Does.StartWith("Add"));
        Assert.That(duplicates[0].Duplicates[0].Method.Name, Does.StartWith("Add"));
    }

    [Test]
    public void FindDuplicates_WithMultipleSimilarMethods_GroupsCorrectly()
    {
        var code1 = @"
public int Add1(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public int Add2(int a, int b)
{
    return a + b;
}";
        
        var code3 = @"
public int Add3(int a, int b)
{
    return a + b;
}";

        var files = CreateParsedFiles(new[] { code1, code2, code3 });
        var detector = new DuplicateDetector(_defaultOptions);
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.95);
        
        Assert.That(duplicates, Has.Count.EqualTo(1));
        Assert.That(duplicates[0].Duplicates, Has.Count.EqualTo(2));
    }

    [Test]
    public void FindDuplicates_CalculatesImpactCorrectly()
    {
        var code1 = @"
public int LongMethod(int a, int b)
{
    var temp1 = a;
    var temp2 = b;
    var temp3 = temp1 + temp2;
    var temp4 = temp3 * 2;
    return temp4;
}";
        
        var code2 = @"
public int AnotherLongMethod(int x, int y)
{
    var val1 = x;
    var val2 = y;
    var val3 = val1 + val2;
    var val4 = val3 * 2;
    return val4;
}";

        var files = CreateParsedFiles(new[] { code1, code2 });
        var detector = new DuplicateDetector(_defaultOptions);
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.8);
        
        Assert.That(duplicates, Has.Count.EqualTo(1));
        Assert.That(duplicates[0].TotalImpact, Is.GreaterThan(0));
        Assert.That(duplicates[0].Duplicates[0].Impact, Is.GreaterThan(0));
    }

    [Test]
    public void FindDuplicates_SortsByImpact()
    {
        var shortCode1 = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var shortCode2 = @"
public int Sum(int a, int b)
{
    return a + b;
}";
        
        var longCode1 = @"
public int LongAdd(int a, int b)
{
    var temp1 = a;
    var temp2 = b;
    var temp3 = temp1 + temp2;
    var temp4 = temp3;
    var temp5 = temp4;
    return temp5;
}";
        
        var longCode2 = @"
public int LongSum(int a, int b)
{
    var val1 = a;
    var val2 = b;
    var val3 = val1 + val2;
    var val4 = val3;
    var val5 = val4;
    return val5;
}";

        var files = CreateParsedFiles(new[] { shortCode1, shortCode2, longCode1, longCode2 });
        var detector = new DuplicateDetector(_defaultOptions);
        
        var duplicates = detector.FindDuplicates(files, threshold: 0.8);
        
        Assert.That(duplicates, Has.Count.EqualTo(2));
        // The group with longer methods should have higher impact and come first
        Assert.That(duplicates[0].TotalImpact, Is.GreaterThan(duplicates[1].TotalImpact));
    }

    [Test]
    public void FindDuplicates_WithNoFiles_ReturnsEmpty()
    {
        var detector = new DuplicateDetector(_defaultOptions);
        
        var duplicates = detector.FindDuplicates(Enumerable.Empty<ParsedFile>(), threshold: 0.8);
        
        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void FindDuplicates_WithHighThreshold_ReturnsFewerDuplicates()
    {
        var code1 = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public int Add(int x, int y)
{
    var result = x + y;
    return result;
}";

        var files = CreateParsedFiles(new[] { code1, code2 });
        var detector = new DuplicateDetector(_defaultOptions);
        
        var lowThresholdDuplicates = detector.FindDuplicates(files, threshold: 0.7);
        var highThresholdDuplicates = detector.FindDuplicates(files, threshold: 0.99);
        
        Assert.That(lowThresholdDuplicates.Count, Is.GreaterThanOrEqualTo(highThresholdDuplicates.Count));
    }

    private List<ParsedFile> CreateParsedFiles(string[] methodCodes)
    {
        var files = new List<ParsedFile>();
        
        for (int i = 0; i < methodCodes.Length; i++)
        {
            var fullCode = $@"
namespace Test
{{
    public class TestClass{i}
    {{
        {methodCodes[i]}
    }}
}}";
            
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, fullCode);
            
            try
            {
                var parsedFile = _parser.ParseFile(tempFile);
                files.Add(parsedFile);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        
        return files;
    }
}