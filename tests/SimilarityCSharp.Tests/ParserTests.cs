using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp.Tests;

[TestFixture]
public class ParserTests
{
    private Parser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new Parser();
    }

    [Test]
    public void ParseFile_WithSimpleMethod_ExtractsMethodCorrectly()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(1));
            Assert.That(parsedFile.Methods[0].Name, Is.EqualTo("Add"));
            Assert.That(parsedFile.Methods[0].ClassName, Is.EqualTo("TestClass"));
            Assert.That(parsedFile.Methods[0].Parameters, Has.Count.EqualTo(2));
            Assert.That(parsedFile.Methods[0].Parameters[0], Is.EqualTo("a"));
            Assert.That(parsedFile.Methods[0].Parameters[1], Is.EqualTo("b"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithMultipleMethods_ExtractsAllMethods()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
        
        public int Multiply(int x, int y)
        {
            return x * y;
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(2));
            Assert.That(parsedFile.Methods.Select(m => m.Name), Contains.Item("Add"));
            Assert.That(parsedFile.Methods.Select(m => m.Name), Contains.Item("Multiply"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithConstructor_ExtractsConstructor()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public TestClass(int value)
        {
            Value = value;
        }
        
        public int Value { get; set; }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            var constructor = parsedFile.Methods.FirstOrDefault(m => m.Name.Contains("ctor"));
            Assert.That(constructor, Is.Not.Null);
            Assert.That(constructor!.Parameters, Has.Count.EqualTo(1));
            Assert.That(constructor.Parameters[0], Is.EqualTo("value"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithPropertyAccessors_ExtractsAccessorsWithBodies()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        private int _value;
        
        public int Value
        {
            get { return _value; }
            set { _value = value; }
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(2));
            Assert.That(parsedFile.Methods.Any(m => m.Name.Contains("Value.get")), Is.True);
            Assert.That(parsedFile.Methods.Any(m => m.Name.Contains("Value.set")), Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithLocalFunction_ExtractsLocalFunction()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public int ProcessData(int[] data)
        {
            int LocalSum(int[] arr)
            {
                return arr.Sum();
            }
            
            return LocalSum(data) * 2;
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(2));
            Assert.That(parsedFile.Methods.Any(m => m.Name == "ProcessData"), Is.True);
            Assert.That(parsedFile.Methods.Any(m => m.Name == "LocalSum"), Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithAsyncMethod_MarksAsAsync()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public async Task<int> ProcessAsync()
        {
            await Task.Delay(100);
            return 42;
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(1));
            Assert.That(parsedFile.Methods[0].IsAsync, Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithAttributes_ExtractsAttributes()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        [Test]
        [Category(""Unit"")]
        public void TestMethod()
        {
            Assert.IsTrue(true);
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(1));
            Assert.That(parsedFile.Methods[0].Attributes, Contains.Item("Test"));
            Assert.That(parsedFile.Methods[0].Attributes, Contains.Item("Category"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithExpressionBodiedMethod_ParsesCorrectly()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public int Square(int x) => x * x;
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(1));
            Assert.That(parsedFile.Methods[0].Name, Is.EqualTo("Square"));
            Assert.That(parsedFile.Methods[0].Parameters, Has.Count.EqualTo(1));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithBothBlockAndArrowMethods_ParsesBoth()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
        
        public int Double(int x) => x * 2;
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Console.WriteLine($"Found {parsedFile.Methods.Count} methods:");
            foreach (var method in parsedFile.Methods)
            {
                Console.WriteLine($"  - {method.Name} (Tokens: {method.Tokens}, Lines: {method.Lines})");
            }
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(2));
            Assert.That(parsedFile.Methods.Any(m => m.Name == "Add"), Is.True);
            Assert.That(parsedFile.Methods.Any(m => m.Name == "Double"), Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithNestedClasses_ExtractsMethodsFromAllClasses()
    {
        var code = @"
namespace Test
{
    public class OuterClass
    {
        public void OuterMethod() { }
        
        public class InnerClass
        {
            public void InnerMethod() { }
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(2));
            Assert.That(parsedFile.Methods.Any(m => m.Name == "OuterMethod"), Is.True);
            Assert.That(parsedFile.Methods.Any(m => m.Name == "InnerMethod"), Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_CalculatesLineAndTokenCounts()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public int ComplexMethod(int a, int b)
        {
            var temp1 = a + b;
            var temp2 = temp1 * 2;
            if (temp2 > 10)
            {
                return temp2;
            }
            return 0;
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(1));
            var method = parsedFile.Methods[0];
            Assert.That(method.Lines, Is.GreaterThan(1));
            Assert.That(method.Tokens, Is.GreaterThan(1));
            Assert.That(method.StartLine, Is.GreaterThan(0));
            Assert.That(method.EndLine, Is.GreaterThan(method.StartLine));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ConvertToTree_WithSimpleExpression_CreatesCorrectTree()
    {
        var code = @"return a + b;";
        var tree = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script));
        var root = tree.GetRoot();
        var statement = root.ChildNodes().First();
        
        var treeNode = _parser.ConvertToTree(statement);
        
        Assert.That(treeNode.Kind, Is.EqualTo(SyntaxKind.ReturnStatement));
        Assert.That(treeNode.Children, Is.Not.Empty);
    }

    [Test]
    public void ConvertToTree_WithIdentifiers_ExtractsValues()
    {
        var code = @"var test = 42;";
        var tree = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script));
        var root = tree.GetRoot();
        var statement = root.ChildNodes().First();
        
        var treeNode = _parser.ConvertToTree(statement);
        
        // Check that identifier values are extracted somewhere in the tree
        var hasIdentifierValue = ContainsValue(treeNode, "test");
        var hasLiteralValue = ContainsValue(treeNode, "42");
        
        Assert.That(hasIdentifierValue, Is.True);
        Assert.That(hasLiteralValue, Is.True);
    }

    [Test]
    public void ParseFile_WithStruct_ExtractsMethodsFromStruct()
    {
        var code = @"
namespace Test
{
    public struct TestStruct
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.EqualTo(1));
            Assert.That(parsedFile.Methods[0].ClassName, Is.EqualTo("TestStruct"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_WithRecord_ExtractsMethodsFromRecord()
    {
        var code = @"
namespace Test
{
    public record TestRecord(int Value)
    {
        public int Double()
        {
            return Value * 2;
        }
    }
}";
        
        var tempFile = CreateTempFile(code);
        
        try
        {
            var parsedFile = _parser.ParseFile(tempFile);
            
            Assert.That(parsedFile.Methods, Has.Count.GreaterThan(0));
            Assert.That(parsedFile.Methods.Any(m => m.ClassName == "TestRecord"), Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private string CreateTempFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private bool ContainsValue(TreeNode node, string value)
    {
        if (node.Value == value)
            return true;
        
        return node.Children.Any(child => ContainsValue(child, value));
    }
}