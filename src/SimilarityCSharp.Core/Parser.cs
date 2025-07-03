using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimilarityCSharp;

public class Parser
{
    int idCounter;

    public ParsedFile ParseFile(string filePath)
    {
        var sourceText = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        var root = tree.GetRoot();
        var methods = ExtractMethods(root, filePath);

        return new ParsedFile
        {
            FilePath = filePath,
            Methods = methods
        };
    }

    List<MethodInfo> ExtractMethods(SyntaxNode root, string filePath)
    {
        var methods = new List<MethodInfo>();
        var walker = new MethodWalker(this, filePath);
        walker.Visit(root);
        methods.AddRange(walker.Methods);
        return methods;
    }

    public TreeNode ConvertToTree(SyntaxNode node)
    {
        var id = Interlocked.Increment(ref idCounter);
        var kind = node.Kind();
        var value = "";
        // Extract values for specific node types
        switch (node)
        {
            case IdentifierNameSyntax identifier:
                value = identifier.Identifier.Text;
                break;
            case LiteralExpressionSyntax literal:
                value = literal.Token.Text;
                break;
            case PredefinedTypeSyntax predefined:
                value = predefined.Keyword.Text;
                break;
        }

        // Check if this is a block with a single statement that should be unwrapped
        if (node is BlockSyntax { Statements.Count: 1 } block && ShouldUnwrapBlock(node.Parent))
        {
            // Skip the block node and directly convert the single statement
            return ConvertToTree(block.Statements[0]);
        }


        var treeNode = new TreeNode(kind, value, id);

        foreach (var child in node.ChildNodes())
        {
            var childTree = ConvertToTree(child);
            treeNode.AddChild(childTree);
        }

        return treeNode;
    }

    bool ShouldUnwrapBlock(SyntaxNode? parent)
    {
        if (parent == null) return false;

        // Unwrap blocks that are children of control flow statements
        return parent.IsKind(SyntaxKind.IfStatement) ||
               parent.IsKind(SyntaxKind.ElseClause) ||
               parent.IsKind(SyntaxKind.WhileStatement) ||
               parent.IsKind(SyntaxKind.ForStatement) ||
               parent.IsKind(SyntaxKind.ForEachStatement) ||
               parent.IsKind(SyntaxKind.DoStatement);
    }

    class MethodWalker(Parser parser, string filePath) : CSharpSyntaxWalker
    {
        readonly Stack<string> classContext = new();

        public List<MethodInfo> Methods { get; } = new();

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            classContext.Push(node.Identifier.Text);
            base.VisitClassDeclaration(node);
            classContext.Pop();
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            classContext.Push(node.Identifier.Text);
            base.VisitStructDeclaration(node);
            classContext.Pop();
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            classContext.Push(node.Identifier.Text);
            base.VisitRecordDeclaration(node);
            classContext.Pop();
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            ProcessMethod(node, node.Identifier.Text, node.Body ?? (SyntaxNode?)node.ExpressionBody);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var name = classContext.Count > 0 ? $"{classContext.Peek()}.ctor" : "ctor";
            ProcessMethod(node, name, node.Body ?? (SyntaxNode?)node.ExpressionBody);
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            ProcessMethod(node, node.Identifier.Text, node.Body ?? (SyntaxNode?)node.ExpressionBody);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // Handle property accessors with bodies
            foreach (var accessor in node.AccessorList?.Accessors ?? [])
            {
                if (accessor.Body != null || accessor.ExpressionBody != null)
                {
                    var accessorName = $"{node.Identifier.Text}.{accessor.Keyword.Text}";
                    ProcessMethod(accessor, accessorName, accessor.Body ?? (SyntaxNode?)accessor.ExpressionBody);
                }
            }

            base.VisitPropertyDeclaration(node);
        }

        void ProcessMethod(SyntaxNode node, string name, SyntaxNode? body)
        {
            if (body == null) return;

            var tree = parser.ConvertToTree(body);
            var lines = GetLineCount(node);
            var tokens = tree.GetSubtreeSize();

            // Extract parameters
            var parameters = new List<string>();
            if (node is BaseMethodDeclarationSyntax method)
            {
                foreach (var param in method.ParameterList.Parameters)
                {
                    parameters.Add(param.Identifier.Text);
                }
            }

            // Check if async
            var isAsync = node.DescendantTokens().Any(t => t.IsKind(SyntaxKind.AsyncKeyword));

            // Get attributes (decorators)
            var attributes = new List<string>();
            if (node is MemberDeclarationSyntax member)
            {
                foreach (var attrList in member.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        attributes.Add(attr.Name.ToString());
                    }
                }
            }

            var methodInfo = new MethodInfo
            {
                Name = name,
                FilePath = filePath,
                StartLine = GetStartLine(node),
                EndLine = GetEndLine(node),
                Lines = lines,
                Tokens = tokens,
                Tree = tree,
                Fingerprint = new AstFingerprint(tree),
                Parameters = parameters,
                IsAsync = isAsync,
                Attributes = attributes,
                ClassName = classContext.Count > 0 ? classContext.Peek() : null
            };

            Methods.Add(methodInfo);
        }

        int GetLineCount(SyntaxNode node)
        {
            var span = node.GetLocation().GetLineSpan();
            return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
        }

        int GetStartLine(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }

        int GetEndLine(SyntaxNode node)
        {
            return node.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
        }
    }
}

public class ParsedFile
{
    public required string FilePath { get; init; }
    public required List<MethodInfo> Methods { get; init; }
}

public class MethodInfo : IEquatable<MethodInfo>
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public required int StartLine { get; init; }
    public required int EndLine { get; init; }
    public required int Lines { get; init; }
    public required int Tokens { get; init; }
    public required TreeNode Tree { get; init; }
    public required AstFingerprint Fingerprint { get; init; }
    public required List<string> Parameters { get; init; }
    public required bool IsAsync { get; init; }
    public required List<string> Attributes { get; init; }
    public string? ClassName { get; init; }

    TsedCalculator.StructuralFeatures? _cachedStructuralFeatures;

    internal TsedCalculator.StructuralFeatures StructuralFeatures
    {
        get
        {
            if (_cachedStructuralFeatures != null)
                return _cachedStructuralFeatures;
            var features = new TsedCalculator.StructuralFeatures();
            TsedCalculator.AnalyzeNode(Tree, features, 0);
            _cachedStructuralFeatures = features;
            return features;
        }
    }

    public string FullName => string.IsNullOrEmpty(ClassName) ? Name : $"{ClassName}.{Name}";

    public bool Equals(MethodInfo? other)
    {
        if (other is null) return false;
        return Name == other.Name;
    }
}