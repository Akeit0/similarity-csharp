using NUnit.Framework;
using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp.Tests;

[TestFixture]
public class TreeNodeTests
{
    [Test]
    public void TreeNode_Constructor_SetsPropertiesCorrectly()
    {
        var node = new TreeNode(SyntaxKind.MethodDeclaration, "TestMethod", 1);
        
        Assert.That(node.Kind, Is.EqualTo(SyntaxKind.MethodDeclaration));
        Assert.That(node.Value, Is.EqualTo("TestMethod"));
        Assert.That(node.Id, Is.EqualTo(1));
        Assert.That(node.Children, Is.Empty);
    }

    [Test]
    public void Label_ForKnownSyntaxKinds_ReturnsCorrectLabels()
    {
        var testCases = new[]
        {
            (SyntaxKind.MethodDeclaration, "Method"),
            (SyntaxKind.LocalFunctionStatement, "Function"),
            (SyntaxKind.ClassDeclaration, "Class"),
            (SyntaxKind.IdentifierName, "Identifier"),
            (SyntaxKind.Block, "Block"),
            (SyntaxKind.ExpressionStatement, "Statement"),
            (SyntaxKind.IfStatement, "If"),
            (SyntaxKind.ForStatement, "For"),
            (SyntaxKind.WhileStatement, "While"),
            (SyntaxKind.VariableDeclaration, "Variable"),
            (SyntaxKind.SimpleAssignmentExpression, "Assignment"),
            (SyntaxKind.AddExpression, "Binary"),
            (SyntaxKind.InvocationExpression, "Call"),
            (SyntaxKind.ReturnStatement, "Return"),
            (SyntaxKind.NumericLiteralExpression, "Literal"),
            (SyntaxKind.StringLiteralExpression, "String"),
            (SyntaxKind.TrueLiteralExpression, "True"),
            (SyntaxKind.FalseLiteralExpression, "False"),
            (SyntaxKind.NullLiteralExpression, "Null")
        };

        foreach (var (kind, expectedLabel) in testCases)
        {
            var node = new TreeNode(kind, "", 1);
            Assert.That(node.Label, Is.EqualTo(expectedLabel), $"Label for {kind} should be {expectedLabel}");
        }
    }

    [Test]
    public void Label_ForUnknownSyntaxKind_ReturnsSyntaxKindString()
    {
        var node = new TreeNode(SyntaxKind.UsingDirective, "", 1);
        
        Assert.That(node.Label, Is.EqualTo("UsingDirective"));
    }

    [Test]
    public void AddChild_AddsChildToCollection()
    {
        var parent = new TreeNode(SyntaxKind.Block, "", 1);
        var child = new TreeNode(SyntaxKind.ReturnStatement, "", 2);
        
        parent.AddChild(child);
        
        Assert.That(parent.Children, Has.Count.EqualTo(1));
        Assert.That(parent.Children[0], Is.EqualTo(child));
    }

    [Test]
    public void AddChild_InvalidatesCachedSize()
    {
        var parent = new TreeNode(SyntaxKind.Block, "", 1);
        var child1 = new TreeNode(SyntaxKind.ReturnStatement, "", 2);
        var child2 = new TreeNode(SyntaxKind.ExpressionStatement, "", 3);
        
        // Calculate size first
        var initialSize = parent.GetSubtreeSize();
        Assert.That(initialSize, Is.EqualTo(1));
        
        // Add child and verify size changes
        parent.AddChild(child1);
        var sizeAfterFirstChild = parent.GetSubtreeSize();
        Assert.That(sizeAfterFirstChild, Is.EqualTo(2));
        
        // Add another child
        parent.AddChild(child2);
        var sizeAfterSecondChild = parent.GetSubtreeSize();
        Assert.That(sizeAfterSecondChild, Is.EqualTo(3));
    }

    [Test]
    public void GetSubtreeSize_SingleNode_Returns1()
    {
        var node = new TreeNode(SyntaxKind.ReturnStatement, "", 1);
        
        Assert.That(node.GetSubtreeSize(), Is.EqualTo(1));
    }

    [Test]
    public void GetSubtreeSize_WithChildren_ReturnsCorrectCount()
    {
        var root = new TreeNode(SyntaxKind.Block, "", 1);
        var child1 = new TreeNode(SyntaxKind.ReturnStatement, "", 2);
        var child2 = new TreeNode(SyntaxKind.ExpressionStatement, "", 3);
        var grandchild = new TreeNode(SyntaxKind.InvocationExpression, "", 4);
        
        child2.AddChild(grandchild);
        root.AddChild(child1);
        root.AddChild(child2);
        
        Assert.That(root.GetSubtreeSize(), Is.EqualTo(4)); // root + child1 + child2 + grandchild
    }

    [Test]
    public void GetSubtreeSize_CachesResult()
    {
        var root = new TreeNode(SyntaxKind.Block, "", 1);
        var child = new TreeNode(SyntaxKind.ReturnStatement, "", 2);
        root.AddChild(child);
        
        // Call multiple times to ensure caching works
        var size1 = root.GetSubtreeSize();
        var size2 = root.GetSubtreeSize();
        var size3 = root.GetSubtreeSize();
        
        Assert.That(size1, Is.EqualTo(2));
        Assert.That(size2, Is.EqualTo(2));
        Assert.That(size3, Is.EqualTo(2));
    }

    [Test]
    public void GetSubtreeSize_DeepTree_CalculatesCorrectly()
    {
        var root = new TreeNode(SyntaxKind.Block, "", 1);
        var current = root;
        
        // Create a deep tree: root -> child1 -> child2 -> ... -> child10
        for (int i = 2; i <= 11; i++)
        {
            var child = new TreeNode(SyntaxKind.ExpressionStatement, "", i);
            current.AddChild(child);
            current = child;
        }
        
        Assert.That(root.GetSubtreeSize(), Is.EqualTo(11));
    }

    [Test]
    public void IsLeaf_NodeWithoutChildren_ReturnsTrue()
    {
        var node = new TreeNode(SyntaxKind.ReturnStatement, "", 1);
        
        Assert.That(node.IsLeaf, Is.True);
    }

    [Test]
    public void IsLeaf_NodeWithChildren_ReturnsFalse()
    {
        var parent = new TreeNode(SyntaxKind.Block, "", 1);
        var child = new TreeNode(SyntaxKind.ReturnStatement, "", 2);
        parent.AddChild(child);
        
        Assert.That(parent.IsLeaf, Is.False);
    }

    [Test]
    public void ToString_WithValue_ReturnsLabelWithValue()
    {
        var node = new TreeNode(SyntaxKind.IdentifierName, "variableName", 1);
        
        Assert.That(node.ToString(), Is.EqualTo("Identifier(variableName)"));
    }

    [Test]
    public void ToString_WithoutValue_ReturnsLabelOnly()
    {
        var node = new TreeNode(SyntaxKind.Block, "", 1);
        
        Assert.That(node.ToString(), Is.EqualTo("Block"));
    }

    [Test]
    public void ToString_WithNullValue_ReturnsLabelOnly()
    {
        var node = new TreeNode(SyntaxKind.Block, "", 1);
        
        Assert.That(node.ToString(), Is.EqualTo("Block"));
    }

    [Test]
    public void TreeNode_ComplexTree_WorksCorrectly()
    {
        // Build a tree representing: if (condition) { return value; }
        var ifStatement = new TreeNode(SyntaxKind.IfStatement, "", 1);
        var condition = new TreeNode(SyntaxKind.IdentifierName, "condition", 2);
        var block = new TreeNode(SyntaxKind.Block, "", 3);
        var returnStatement = new TreeNode(SyntaxKind.ReturnStatement, "", 4);
        var returnValue = new TreeNode(SyntaxKind.IdentifierName, "value", 5);
        
        returnStatement.AddChild(returnValue);
        block.AddChild(returnStatement);
        ifStatement.AddChild(condition);
        ifStatement.AddChild(block);
        
        Assert.That(ifStatement.GetSubtreeSize(), Is.EqualTo(5));
        Assert.That(ifStatement.IsLeaf, Is.False);
        Assert.That(returnValue.IsLeaf, Is.True);
        Assert.That(ifStatement.Label, Is.EqualTo("If"));
        Assert.That(condition.ToString(), Is.EqualTo("Identifier(condition)"));
    }

    [Test]
    public void TreeNode_WithLiteralValues_WorksCorrectly()
    {
        var testCases = new[]
        {
            (SyntaxKind.NumericLiteralExpression, "42", "Literal(42)"),
            (SyntaxKind.StringLiteralExpression, "\"hello\"", "String(\"hello\")"),
            (SyntaxKind.TrueLiteralExpression, "true", "True(true)"),
            (SyntaxKind.FalseLiteralExpression, "false", "False(false)"),
            (SyntaxKind.NullLiteralExpression, "null", "Null(null)")
        };

        foreach (var (kind, value, expectedString) in testCases)
        {
            var node = new TreeNode(kind, value, 1);
            Assert.That(node.ToString(), Is.EqualTo(expectedString));
            Assert.That(node.IsLeaf, Is.True);
        }
    }

    [Test]
    public void TreeNode_IdProperty_IsImmutable()
    {
        var node = new TreeNode(SyntaxKind.Block, "", 42);
        
        Assert.That(node.Id, Is.EqualTo(42));
    }

    [Test]
    public void TreeNode_KindProperty_IsImmutable()
    {
        var node = new TreeNode(SyntaxKind.MethodDeclaration, "", 1);
        
        Assert.That(node.Kind, Is.EqualTo(SyntaxKind.MethodDeclaration));
    }

    [Test]
    public void TreeNode_ValueProperty_IsImmutable()
    {
        var node = new TreeNode(SyntaxKind.IdentifierName, "test", 1);
        
        Assert.That(node.Value, Is.EqualTo("test"));
    }

    [Test]
    public void TreeNode_ChildrenCollection_IsNotNull()
    {
        var node = new TreeNode(SyntaxKind.Block, "", 1);
        
        Assert.That(node.Children, Is.Not.Null);
        Assert.That(node.Children, Is.Empty);
    }
}