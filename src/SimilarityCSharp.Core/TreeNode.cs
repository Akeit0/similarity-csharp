using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp;

public class TreeNode(SyntaxKind kind, string value, int id)
{
    public SyntaxKind Kind { get; } = kind;
    public string Value { get; } = value;
    public int Id { get; } = id;
    public List<TreeNode> Children { get; } = new();

    int? cachedSize;

    // Backward compatibility property
    public string Label => Kind switch
    {
        SyntaxKind.MethodDeclaration => "Method",
        SyntaxKind.LocalFunctionStatement => "Function",
        SyntaxKind.ClassDeclaration => "Class",
        SyntaxKind.IdentifierName => "Identifier",
        SyntaxKind.Block => "Block",
        SyntaxKind.ExpressionStatement => "Statement",
        SyntaxKind.IfStatement => "If",
        SyntaxKind.ForStatement => "For",
        SyntaxKind.WhileStatement => "While",
        SyntaxKind.VariableDeclaration => "Variable",
        SyntaxKind.SimpleAssignmentExpression => "Assignment",
        SyntaxKind.AddExpression => "Binary",
        SyntaxKind.InvocationExpression => "Call",
        SyntaxKind.ReturnStatement => "Return",
        SyntaxKind.NumericLiteralExpression => "Literal",
        SyntaxKind.StringLiteralExpression => "String",
        SyntaxKind.TrueLiteralExpression => "True",
        SyntaxKind.FalseLiteralExpression => "False",
        SyntaxKind.NullLiteralExpression => "Null",
        _ => Kind.ToString()
    };

    public void AddChild(TreeNode child)
    {
        Children.Add(child);
        cachedSize = null; // Invalidate cache
    }

    public int GetSubtreeSize()
    {
        if (cachedSize.HasValue)
            return cachedSize.Value;

        int size = 1; // Count this node
        foreach (var child in Children)
        {
            size += child.GetSubtreeSize();
        }

        cachedSize = size;
        return size;
    }

    public bool IsLeaf => Children.Count == 0;

    public override string ToString()
    {
        return string.IsNullOrEmpty(Value) ? Label : $"{Label}({Value})";
    }
}