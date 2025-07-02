using NUnit.Framework;

namespace SimilarityCSharp.Tests;

[TestFixture]
public class TSEDCalculatorTests
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
    public void CalculateSimilarity_IdenticalTrees_Returns1()
    {
        var code = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var tree1 = ParseMethodToTree(code);
        var tree2 = ParseMethodToTree(code);
        
        var similarity = TsedCalculator.CalculateSimilarity(tree1, tree2, _defaultOptions);
        
        Assert.That(similarity, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void CalculateSimilarity_CompletelyDifferentTrees_ReturnsLowSimilarity()
    {
        var code1 = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public string GetName()
{
    if (true)
    {
        return ""test"";
    }
    return ""default"";
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var similarity = TsedCalculator.CalculateSimilarity(tree1, tree2, _defaultOptions);
        
        Assert.That(similarity, Is.LessThan(0.3));
    }

    [Test]
    public void CalculateSimilarity_SimilarWithVariableRename_ReturnsHighSimilarity()
    {
        var code1 = @"
public int Add(int a, int b)
{
    var result = a + b;
    return result;
}";
        
        var code2 = @"
public int Add(int x, int y)
{
    var sum = x + y;
    return sum;
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var similarity = TsedCalculator.CalculateSimilarity(tree1, tree2, _defaultOptions);
        
        Assert.That(similarity, Is.GreaterThan(0.8));
    }

    [Test]
    public void CalculateSimilarity_DifferentSizeTrees_AppliesSizePenalty()
    {
        var code1 = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public int Add(int a, int b)
{
    var temp1 = a;
    var temp2 = b;
    var temp3 = temp1 + temp2;
    var temp4 = temp3;
    var temp5 = temp4;
    return temp5;
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var similarity = TsedCalculator.CalculateSimilarity(tree1, tree2, _defaultOptions);
        
        Assert.That(similarity, Is.LessThan(0.7)); // Should be penalized for size difference
    }

    [Test]
    public void CalculateSimilarity_WithoutSizePenalty_HigherSimilarity()
    {
        var optionsWithoutPenalty = new TsedOptions
        {
            MinLines = 1,
            MinTokens = 0,
            SizePenalty = false
        };
        
        var code1 = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public int Add(int a, int b)
{
    var temp = a + b;
    return temp;
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var similarityWithPenalty = TsedCalculator.CalculateSimilarity(tree1, tree2, _defaultOptions);
        var similarityWithoutPenalty = TsedCalculator.CalculateSimilarity(tree1, tree2, optionsWithoutPenalty);
        
        Assert.That(similarityWithoutPenalty, Is.GreaterThan(similarityWithPenalty));
    }

    [Test]
    public void CalculateSimilarity_EmptyTrees_Returns1()
    {
        var emptyTree1 = new TreeNode(Microsoft.CodeAnalysis.CSharp.SyntaxKind.Block, "", 1);
        var emptyTree2 = new TreeNode(Microsoft.CodeAnalysis.CSharp.SyntaxKind.Block, "", 2);
        
        var similarity = TsedCalculator.CalculateSimilarity(emptyTree1, emptyTree2, _defaultOptions);
        
        Assert.That(similarity, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void CalculateSimilarity_SingleNodeTrees_Returns1()
    {
        var tree1 = new TreeNode(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ReturnStatement, "return", 1);
        var tree2 = new TreeNode(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ReturnStatement, "return", 2);
        
        var similarity = TsedCalculator.CalculateSimilarity(tree1, tree2, _defaultOptions);
        
        Assert.That(similarity, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void CalculateSimilarity_StructuralChanges_ReflectsInSimilarity()
    {
        var code1 = @"
public int Process(int value)
{
    if (value > 0)
        return value * 2;
    return 0;
}";
        
        var code2 = @"
public int Process(int value)
{
    if (value > 0)
    {
        return value * 2;
    }
    else
    {
        return 0;
    }
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var similarity = TsedCalculator.CalculateSimilarity(tree1, tree2, _defaultOptions);
        
        Assert.That(similarity, Is.GreaterThan(0.7));
        Assert.That(similarity, Is.LessThan(1.0));
    }

    private TreeNode ParseMethodToTree(string code)
    {
        var fullCode = $@"
namespace Test
{{
    public class TestClass
    {{
        {code}
    }}
}}";
        
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, fullCode);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            return parsedFile.Methods.First().Tree;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}