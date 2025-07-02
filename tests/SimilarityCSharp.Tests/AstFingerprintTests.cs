using NUnit.Framework;
using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp.Tests;

[TestFixture]
public class ASTFingerprintTests
{
    private Parser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new Parser();
    }

    [Test]
    public void ASTFingerprint_IdenticalTrees_ReturnsSimilarity1()
    {
        var code = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var tree1 = ParseMethodToTree(code);
        var tree2 = ParseMethodToTree(code);
        
        var fingerprint1 = new AstFingerprint(tree1);
        var fingerprint2 = new AstFingerprint(tree2);
        
        var similarity = fingerprint1.CalculateSimilarity(fingerprint2);
        
        Assert.That(similarity, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void ASTFingerprint_SimilarTrees_ReturnsHighSimilarity()
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
        
        var fingerprint1 = new AstFingerprint(tree1);
        var fingerprint2 = new AstFingerprint(tree2);
        
        var similarity = fingerprint1.CalculateSimilarity(fingerprint2);
        
        Assert.That(similarity, Is.GreaterThan(0.8));
    }

    [Test]
    public void ASTFingerprint_DifferentTrees_ReturnsLowSimilarity()
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
        return ""test"";
    return ""default"";
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var fingerprint1 = new AstFingerprint(tree1);
        var fingerprint2 = new AstFingerprint(tree2);
        
        var similarity = fingerprint1.CalculateSimilarity(fingerprint2);
        
        Assert.That(similarity, Is.LessThan(0.5));
    }

    [Test]
    public void MightBeSimilar_SimilarTrees_ReturnsTrue()
    {
        var code1 = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public int Sum(int x, int y)
{
    return x + y;
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var fingerprint1 = new AstFingerprint(tree1);
        var fingerprint2 = new AstFingerprint(tree2);
        
        Assert.That(fingerprint1.MightBeSimilar(fingerprint2, 0.3), Is.True);
    }

    [Test]
    public void MightBeSimilar_VeryDifferentTrees_ReturnsFalse()
    {
        var code1 = @"
public int SimpleAdd(int a, int b)
{
    return a + b;
}";
        
        var code2 = @"
public async Task<string> ComplexMethodWithLoops()
{
    var list = new List<string>();
    for (int i = 0; i < 100; i++)
    {
        if (i % 2 == 0)
        {
            list.Add(i.ToString());
        }
        else
        {
            await Task.Delay(1);
        }
    }
    return string.Join("","", list);
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var fingerprint1 = new AstFingerprint(tree1);
        var fingerprint2 = new AstFingerprint(tree2);
        
        Assert.That(fingerprint1.MightBeSimilar(fingerprint2, 0.3), Is.False);
    }

    [Test]
    public void MightBeSimilar_WithEmptyFingerprint_ReturnsTrue()
    {
        var code = @"
public int Add(int a, int b)
{
    return a + b;
}";
        
        var tree = ParseMethodToTree(code);
        var emptyTree = new TreeNode(SyntaxKind.Block, "", 1);
        
        var fingerprint = new AstFingerprint(tree);
        var emptyFingerprint = new AstFingerprint(emptyTree);
        
        Assert.That(fingerprint.MightBeSimilar(emptyFingerprint), Is.True);
        Assert.That(emptyFingerprint.MightBeSimilar(fingerprint), Is.True);
    }

    [Test]
    public void ASTFingerprint_WithControlFlowNodes_WeightsCorrectly()
    {
        var codeWithIf = @"
public int Process(int value)
{
    if (value > 0)
        return value * 2;
    return 0;
}";
        
        var codeWithFor = @"
public int Process(int value)
{
    for (int i = 0; i < value; i++)
        value += i;
    return value;
}";
        
        var tree1 = ParseMethodToTree(codeWithIf);
        var tree2 = ParseMethodToTree(codeWithFor);
        
        var fingerprint1 = new AstFingerprint(tree1);
        var fingerprint2 = new AstFingerprint(tree2);
        
        var similarity = fingerprint1.CalculateSimilarity(fingerprint2);
        
        // Should have some similarity due to both having control flow, but not high
        Assert.That(similarity, Is.GreaterThan(0.2));
        Assert.That(similarity, Is.LessThan(0.8));
    }

    [Test]
    public void ASTFingerprint_WithMethodCalls_CalculatesCorrectly()
    {
        var code1 = @"
public void Process()
{
    Console.WriteLine(""test"");
    var result = Calculate(5);
}";
        
        var code2 = @"
public void Execute()
{
    Debug.Print(""debug"");
    var value = Compute(10);
}";
        
        var tree1 = ParseMethodToTree(code1);
        var tree2 = ParseMethodToTree(code2);
        
        var fingerprint1 = new AstFingerprint(tree1);
        var fingerprint2 = new AstFingerprint(tree2);
        
        var similarity = fingerprint1.CalculateSimilarity(fingerprint2);
        
        // Should have good similarity due to similar structure (method calls, variable declarations)
        Assert.That(similarity, Is.GreaterThan(0.6));
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

[TestFixture]
public class BloomFilter128Tests
{
    [Test]
    public void BloomFilter128_Empty_HasNoBitsSet()
    {
        var filter = BloomFilter128.Empty;
        
        Assert.That(filter.PopCount(), Is.EqualTo(0));
        Assert.That(filter.IsEmpty, Is.True);
    }

    [Test]
    public void BloomFilter128_SetBit_SetsCorrectBit()
    {
        var filter = BloomFilter128.Empty;
        var updatedFilter = filter.SetBit(10);
        
        Assert.That(updatedFilter.PopCount(), Is.EqualTo(1));
        Assert.That(updatedFilter.IsEmpty, Is.False);
    }

    [Test]
    public void BloomFilter128_SetMultipleBits_CountsCorrectly()
    {
        var filter = BloomFilter128.Empty;
        filter = filter.SetBit(0);
        filter = filter.SetBit(64);
        filter = filter.SetBit(127);
        
        Assert.That(filter.PopCount(), Is.EqualTo(3));
    }

    [Test]
    public void BloomFilter128_SetSameBitTwice_CountsOnce()
    {
        var filter = BloomFilter128.Empty;
        filter = filter.SetBit(10);
        filter = filter.SetBit(10);
        
        Assert.That(filter.PopCount(), Is.EqualTo(1));
    }

    [Test]
    public void BloomFilter128_Union_CombinesBits()
    {
        var filter1 = BloomFilter128.Empty.SetBit(10).SetBit(20);
        var filter2 = BloomFilter128.Empty.SetBit(20).SetBit(30);
        
        var union = filter1.Union(filter2);
        
        Assert.That(union.PopCount(), Is.EqualTo(3)); // bits 10, 20, 30
    }

    [Test]
    public void BloomFilter128_Intersect_FindsCommonBits()
    {
        var filter1 = BloomFilter128.Empty.SetBit(10).SetBit(20).SetBit(30);
        var filter2 = BloomFilter128.Empty.SetBit(20).SetBit(30).SetBit(40);
        
        var intersection = filter1.Intersect(filter2);
        
        Assert.That(intersection.PopCount(), Is.EqualTo(2)); // bits 20, 30
    }

    [Test]
    public void BloomFilter128_IntersectEmpty_ReturnsEmpty()
    {
        var filter = BloomFilter128.Empty.SetBit(10).SetBit(20);
        var empty = BloomFilter128.Empty;
        
        var intersection = filter.Intersect(empty);
        
        Assert.That(intersection.PopCount(), Is.EqualTo(0));
        Assert.That(intersection.IsEmpty, Is.True);
    }

    [Test]
    public void BloomFilter128_SetBitOutOfRange_WrapsCorrectly()
    {
        var filter = BloomFilter128.Empty;
        // Test with values that should wrap around due to & 0x7F
        filter = filter.SetBit(128); // Should become bit 0
        filter = filter.SetBit(255); // Should become bit 127
        
        Assert.That(filter.PopCount(), Is.EqualTo(2));
    }

    [Test]
    public void BloomFilter128_PopCount_HandlesBothHalves()
    {
        var filter = BloomFilter128.Empty;
        
        // Set bits in lower 64 bits
        for (int i = 0; i < 32; i++)
        {
            filter = filter.SetBit(i);
        }
        
        // Set bits in upper 64 bits
        for (int i = 64; i < 96; i++)
        {
            filter = filter.SetBit(i);
        }
        
        Assert.That(filter.PopCount(), Is.EqualTo(64));
    }
}