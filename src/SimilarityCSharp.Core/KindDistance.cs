using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp;

/// <summary>
/// Categories for grouping similar SyntaxKind values to calculate semantic distance.
/// </summary>
internal enum KindCategory : byte
{
    /// <summary>Unknown or unmapped syntax kind.</summary>
    Unknown = 0,
    
    /// <summary>Numeric literals (int, float, decimal, etc.).</summary>
    NumericLiteral = 1,
    
    /// <summary>String and UTF8 string literals.</summary>
    StringLiteral = 2,
    
    /// <summary>Character literals.</summary>
    CharLiteral = 3,
    
    /// <summary>Boolean literals (true, false).</summary>
    BoolLiteral = 4,
    
    /// <summary>Null and default literals.</summary>
    NullLiteral = 5,
    
    /// <summary>Simple identifier names.</summary>
    SimpleIdentifier = 6,
    
    /// <summary>Qualified names (A.B.C).</summary>
    QualifiedIdentifier = 7,
    
    /// <summary>Generic names (List&lt;T&gt;).</summary>
    GenericIdentifier = 8,
    
    /// <summary>This and base expressions.</summary>
    ThisBaseIdentifier = 9,
    
    /// <summary>Addition and subtraction operators.</summary>
    AdditiveOperator = 10,
    
    /// <summary>Multiplication, division, and modulo operators.</summary>
    MultiplicativeOperator = 11,
    
    /// <summary>Unary plus and minus operators.</summary>
    UnaryArithmeticOperator = 12,
    
    /// <summary>Increment and decrement operators.</summary>
    IncrementOperator = 13,
    
    /// <summary>Logical AND and OR operators.</summary>
    BinaryLogicalOperator = 14,
    
    /// <summary>Logical NOT operator.</summary>
    UnaryLogicalOperator = 15,
    
    /// <summary>Binary bitwise operators (AND, OR, XOR).</summary>
    BinaryBitwiseOperator = 16,
    
    /// <summary>Bitwise NOT operator.</summary>
    UnaryBitwiseOperator = 17,
    
    /// <summary>Bit shift operators.</summary>
    ShiftOperator = 18,
    
    /// <summary>Equality operators (==, !=).</summary>
    EqualityOperator = 19,
    
    /// <summary>Relational operators (&lt;, &gt;, &lt;=, &gt;=).</summary>
    RelationalOperator = 20,
    
    /// <summary>Type checking operators (is, as).</summary>
    TypeCheckOperator = 21,
    
    /// <summary>Simple assignment operator (=).</summary>
    SimpleAssignment = 22,
    
    /// <summary>Compound assignment operators (+=, -=, etc.).</summary>
    CompoundAssignment = 23,
    
    /// <summary>Loop statements (for, while, foreach, do).</summary>
    LoopStatement = 24,
    
    /// <summary>Conditional statements and expressions (if, conditional).</summary>
    ConditionalStatement = 25,
    
    /// <summary>Switch statements and expressions.</summary>
    SwitchStatement = 26,
    
    /// <summary>Else clause.</summary>
    ElseClause = 27,
    
    /// <summary>Break and continue statements.</summary>
    LoopControlStatement = 28,
    
    /// <summary>Return statement.</summary>
    ReturnStatement = 29,
    
    /// <summary>Goto statement.</summary>
    GotoStatement = 30,
    
    /// <summary>Exception handling statements (try, catch, finally, throw).</summary>
    ExceptionStatement = 31,
    
    /// <summary>Method invocation expressions.</summary>
    MethodInvocation = 32,
    
    /// <summary>Property and field access expressions.</summary>
    PropertyAccess = 33,
    
    /// <summary>Element access expressions (indexers).</summary>
    ElementAccess = 34,
    
    /// <summary>Object creation expressions.</summary>
    ObjectCreation = 35,
    
    /// <summary>Array creation expressions.</summary>
    ArrayCreation = 36,
    
    /// <summary>Type operation expressions (cast, typeof, sizeof, etc.).</summary>
    TypeOperation = 37,
    
    /// <summary>Declaration statements and syntax.</summary>
    Declaration = 38,
    
    /// <summary>Structural syntax (blocks, lists, parentheses).</summary>
    Structural = 39
}

internal static class KindDistance
{
    private static readonly KindCategory[] kindCategories = new KindCategory[9100];
    
    // Triangular array: only store upper triangle where cat1 <= cat2
    private static readonly byte[] categoryDistances = new byte[40 * 41 / 2];

    static KindDistance()
    {
        InitializeKindCategories();
        InitializeCategoryDistances();
    }

    public static double CalculateDistance(SyntaxKind kind1, SyntaxKind kind2)
    {
        if (kind1 == kind2) return 0.0;

        var cat1 = (int)kindCategories[(int)kind1];
        var cat2 = (int)kindCategories[(int)kind2];
        
        var index = GetTriangularIndex(cat1, cat2);
        return categoryDistances[index] * 0.01; // Convert back to 0.0-1.0 range
    }

    private static void InitializeKindCategories()
    {
        // Literals
        kindCategories[(int)SyntaxKind.NumericLiteralExpression] = KindCategory.NumericLiteral;
        kindCategories[(int)SyntaxKind.StringLiteralExpression] = KindCategory.StringLiteral;
        kindCategories[(int)SyntaxKind.CharacterLiteralExpression] = KindCategory.CharLiteral;
        kindCategories[(int)SyntaxKind.TrueLiteralExpression] = KindCategory.BoolLiteral;
        kindCategories[(int)SyntaxKind.FalseLiteralExpression] = KindCategory.BoolLiteral;
        kindCategories[(int)SyntaxKind.NullLiteralExpression] = KindCategory.NullLiteral;
        kindCategories[(int)SyntaxKind.DefaultLiteralExpression] = KindCategory.NullLiteral;
        kindCategories[(int)SyntaxKind.Utf8StringLiteralExpression] = KindCategory.StringLiteral;

        // Identifiers
        kindCategories[(int)SyntaxKind.IdentifierName] = KindCategory.SimpleIdentifier;
        kindCategories[(int)SyntaxKind.QualifiedName] = KindCategory.QualifiedIdentifier;
        kindCategories[(int)SyntaxKind.GenericName] = KindCategory.GenericIdentifier;
        kindCategories[(int)SyntaxKind.AliasQualifiedName] = KindCategory.QualifiedIdentifier;
        kindCategories[(int)SyntaxKind.ThisExpression] = KindCategory.ThisBaseIdentifier;
        kindCategories[(int)SyntaxKind.BaseExpression] = KindCategory.ThisBaseIdentifier;

        // Arithmetic operators
        kindCategories[(int)SyntaxKind.AddExpression] = KindCategory.AdditiveOperator;
        kindCategories[(int)SyntaxKind.SubtractExpression] = KindCategory.AdditiveOperator;
        kindCategories[(int)SyntaxKind.MultiplyExpression] = KindCategory.MultiplicativeOperator;
        kindCategories[(int)SyntaxKind.DivideExpression] = KindCategory.MultiplicativeOperator;
        kindCategories[(int)SyntaxKind.ModuloExpression] = KindCategory.MultiplicativeOperator;
        kindCategories[(int)SyntaxKind.UnaryPlusExpression] = KindCategory.UnaryArithmeticOperator;
        kindCategories[(int)SyntaxKind.UnaryMinusExpression] = KindCategory.UnaryArithmeticOperator;
        kindCategories[(int)SyntaxKind.PreIncrementExpression] = KindCategory.IncrementOperator;
        kindCategories[(int)SyntaxKind.PreDecrementExpression] = KindCategory.IncrementOperator;
        kindCategories[(int)SyntaxKind.PostIncrementExpression] = KindCategory.IncrementOperator;
        kindCategories[(int)SyntaxKind.PostDecrementExpression] = KindCategory.IncrementOperator;

        // Logical operators
        kindCategories[(int)SyntaxKind.LogicalAndExpression] = KindCategory.BinaryLogicalOperator;
        kindCategories[(int)SyntaxKind.LogicalOrExpression] = KindCategory.BinaryLogicalOperator;
        kindCategories[(int)SyntaxKind.LogicalNotExpression] = KindCategory.UnaryLogicalOperator;

        // Bitwise operators
        kindCategories[(int)SyntaxKind.BitwiseAndExpression] = KindCategory.BinaryBitwiseOperator;
        kindCategories[(int)SyntaxKind.BitwiseOrExpression] = KindCategory.BinaryBitwiseOperator;
        kindCategories[(int)SyntaxKind.ExclusiveOrExpression] = KindCategory.BinaryBitwiseOperator;
        kindCategories[(int)SyntaxKind.BitwiseNotExpression] = KindCategory.UnaryBitwiseOperator;
        kindCategories[(int)SyntaxKind.LeftShiftExpression] = KindCategory.ShiftOperator;
        kindCategories[(int)SyntaxKind.RightShiftExpression] = KindCategory.ShiftOperator;
        kindCategories[(int)SyntaxKind.UnsignedRightShiftExpression] = KindCategory.ShiftOperator;

        // Comparison operators
        kindCategories[(int)SyntaxKind.EqualsExpression] = KindCategory.EqualityOperator;
        kindCategories[(int)SyntaxKind.NotEqualsExpression] = KindCategory.EqualityOperator;
        kindCategories[(int)SyntaxKind.LessThanExpression] = KindCategory.RelationalOperator;
        kindCategories[(int)SyntaxKind.LessThanOrEqualExpression] = KindCategory.RelationalOperator;
        kindCategories[(int)SyntaxKind.GreaterThanExpression] = KindCategory.RelationalOperator;
        kindCategories[(int)SyntaxKind.GreaterThanOrEqualExpression] = KindCategory.RelationalOperator;
        kindCategories[(int)SyntaxKind.IsExpression] = KindCategory.TypeCheckOperator;
        kindCategories[(int)SyntaxKind.AsExpression] = KindCategory.TypeCheckOperator;

        // Assignment operators
        kindCategories[(int)SyntaxKind.SimpleAssignmentExpression] = KindCategory.SimpleAssignment;
        kindCategories[(int)SyntaxKind.AddAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.SubtractAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.MultiplyAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.DivideAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.ModuloAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.AndAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.ExclusiveOrAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.OrAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.LeftShiftAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.RightShiftAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.CoalesceAssignmentExpression] = KindCategory.CompoundAssignment;
        kindCategories[(int)SyntaxKind.UnsignedRightShiftAssignmentExpression] = KindCategory.CompoundAssignment;

        // Control flow - loops
        kindCategories[(int)SyntaxKind.WhileStatement] = KindCategory.LoopStatement;
        kindCategories[(int)SyntaxKind.ForStatement] = KindCategory.LoopStatement;
        kindCategories[(int)SyntaxKind.ForEachStatement] = KindCategory.LoopStatement;
        kindCategories[(int)SyntaxKind.DoStatement] = KindCategory.LoopStatement;

        // Control flow - conditionals
        kindCategories[(int)SyntaxKind.IfStatement] = KindCategory.ConditionalStatement;
        kindCategories[(int)SyntaxKind.ConditionalExpression] = KindCategory.ConditionalStatement;
        kindCategories[(int)SyntaxKind.SwitchStatement] = KindCategory.SwitchStatement;
        kindCategories[(int)SyntaxKind.SwitchExpression] = KindCategory.SwitchStatement;
        kindCategories[(int)SyntaxKind.ElseClause] = KindCategory.ElseClause;

        // Control flow - jumps
        kindCategories[(int)SyntaxKind.BreakStatement] = KindCategory.LoopControlStatement;
        kindCategories[(int)SyntaxKind.ContinueStatement] = KindCategory.LoopControlStatement;
        kindCategories[(int)SyntaxKind.ReturnStatement] = KindCategory.ReturnStatement;
        kindCategories[(int)SyntaxKind.GotoStatement] = KindCategory.GotoStatement;

        // Exception handling
        kindCategories[(int)SyntaxKind.TryStatement] = KindCategory.ExceptionStatement;
        kindCategories[(int)SyntaxKind.CatchClause] = KindCategory.ExceptionStatement;
        kindCategories[(int)SyntaxKind.FinallyClause] = KindCategory.ExceptionStatement;
        kindCategories[(int)SyntaxKind.ThrowStatement] = KindCategory.ExceptionStatement;

        // Member access
        kindCategories[(int)SyntaxKind.InvocationExpression] = KindCategory.MethodInvocation;
        kindCategories[(int)SyntaxKind.SimpleMemberAccessExpression] = KindCategory.PropertyAccess;
        kindCategories[(int)SyntaxKind.PointerMemberAccessExpression] = KindCategory.PropertyAccess;
        kindCategories[(int)SyntaxKind.ConditionalAccessExpression] = KindCategory.PropertyAccess;
        kindCategories[(int)SyntaxKind.ElementAccessExpression] = KindCategory.ElementAccess;

        // Object creation
        kindCategories[(int)SyntaxKind.ObjectCreationExpression] = KindCategory.ObjectCreation;
        kindCategories[(int)SyntaxKind.ImplicitObjectCreationExpression] = KindCategory.ObjectCreation;
        kindCategories[(int)SyntaxKind.AnonymousObjectCreationExpression] = KindCategory.ObjectCreation;
        kindCategories[(int)SyntaxKind.ArrayCreationExpression] = KindCategory.ArrayCreation;
        kindCategories[(int)SyntaxKind.ImplicitArrayCreationExpression] = KindCategory.ArrayCreation;
        kindCategories[(int)SyntaxKind.StackAllocArrayCreationExpression] = KindCategory.ArrayCreation;
        kindCategories[(int)SyntaxKind.CollectionExpression] = KindCategory.ArrayCreation;

        // Type operations
        kindCategories[(int)SyntaxKind.CastExpression] = KindCategory.TypeOperation;
        kindCategories[(int)SyntaxKind.TypeOfExpression] = KindCategory.TypeOperation;
        kindCategories[(int)SyntaxKind.SizeOfExpression] = KindCategory.TypeOperation;
        kindCategories[(int)SyntaxKind.DefaultExpression] = KindCategory.TypeOperation;
        kindCategories[(int)SyntaxKind.CheckedExpression] = KindCategory.TypeOperation;
        kindCategories[(int)SyntaxKind.UncheckedExpression] = KindCategory.TypeOperation;

        // Declarations
        kindCategories[(int)SyntaxKind.VariableDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.LocalDeclarationStatement] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.FieldDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.PropertyDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.MethodDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.ConstructorDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.DestructorDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.ClassDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.StructDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.InterfaceDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.EnumDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.DelegateDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.RecordDeclaration] = KindCategory.Declaration;
        kindCategories[(int)SyntaxKind.RecordStructDeclaration] = KindCategory.Declaration;

        // Structural
        kindCategories[(int)SyntaxKind.Block] = KindCategory.Structural;
        kindCategories[(int)SyntaxKind.ArgumentList] = KindCategory.Structural;
        kindCategories[(int)SyntaxKind.ParameterList] = KindCategory.Structural;
        kindCategories[(int)SyntaxKind.BracketedArgumentList] = KindCategory.Structural;
        kindCategories[(int)SyntaxKind.BracketedParameterList] = KindCategory.Structural;
        kindCategories[(int)SyntaxKind.TypeArgumentList] = KindCategory.Structural;
        kindCategories[(int)SyntaxKind.TypeParameterList] = KindCategory.Structural;
        kindCategories[(int)SyntaxKind.ParenthesizedExpression] = KindCategory.Structural;
    }

    private static void InitializeCategoryDistances()
    {
        // Initialize all distances to default high value (100)
        for (int i = 0; i < categoryDistances.Length; i++)
        {
            categoryDistances[i] = 100;
        }

        // Same category distances (diagonal)
        for (int i = 0; i < 40; i++)
        {
            var index = GetTriangularIndex(i, i);
            categoryDistances[index] = 0;
        }

        // Similar literal types
        SetDistance(KindCategory.StringLiteral, KindCategory.CharLiteral, 10);
        SetDistance(KindCategory.BoolLiteral, KindCategory.NullLiteral, 15);

        // Identifier variations
        SetDistance(KindCategory.SimpleIdentifier, KindCategory.QualifiedIdentifier, 5);
        SetDistance(KindCategory.SimpleIdentifier, KindCategory.GenericIdentifier, 10);
        SetDistance(KindCategory.QualifiedIdentifier, KindCategory.GenericIdentifier, 5);
        SetDistance(KindCategory.SimpleIdentifier, KindCategory.ThisBaseIdentifier, 20);

        // Arithmetic operator groups
        SetDistance(KindCategory.AdditiveOperator, KindCategory.MultiplicativeOperator, 10);
        SetDistance(KindCategory.AdditiveOperator, KindCategory.UnaryArithmeticOperator, 15);
        SetDistance(KindCategory.AdditiveOperator, KindCategory.IncrementOperator, 20);

        // Logical operators
        SetDistance(KindCategory.BinaryLogicalOperator, KindCategory.UnaryLogicalOperator, 10);

        // Bitwise operators
        SetDistance(KindCategory.BinaryBitwiseOperator, KindCategory.UnaryBitwiseOperator, 10);
        SetDistance(KindCategory.BinaryBitwiseOperator, KindCategory.ShiftOperator, 15);

        // Comparison operators
        SetDistance(KindCategory.EqualityOperator, KindCategory.RelationalOperator, 10);
        SetDistance(KindCategory.EqualityOperator, KindCategory.TypeCheckOperator, 20);

        // Assignment operators
        SetDistance(KindCategory.SimpleAssignment, KindCategory.CompoundAssignment, 10);

        // Control flow
        SetDistance(KindCategory.LoopStatement, KindCategory.ConditionalStatement, 15);
        SetDistance(KindCategory.ConditionalStatement, KindCategory.SwitchStatement, 10);
        SetDistance(KindCategory.ConditionalStatement, KindCategory.ElseClause, 5);
        SetDistance(KindCategory.LoopControlStatement, KindCategory.ReturnStatement, 10);

        // Member access
        SetDistance(KindCategory.MethodInvocation, KindCategory.PropertyAccess, 10);
        SetDistance(KindCategory.PropertyAccess, KindCategory.ElementAccess, 5);

        // Object creation
        SetDistance(KindCategory.ObjectCreation, KindCategory.ArrayCreation, 15);

        // Cross-category distances (higher)
        SetDistance(KindCategory.NumericLiteral, KindCategory.SimpleIdentifier, 40);
        SetDistance(KindCategory.SimpleIdentifier, KindCategory.MethodInvocation, 30);
        SetDistance(KindCategory.AdditiveOperator, KindCategory.Declaration, 70);
        SetDistance(KindCategory.LoopStatement, KindCategory.Declaration, 60);
        SetDistance(KindCategory.NumericLiteral, KindCategory.Declaration, 80);
        SetDistance(KindCategory.MethodInvocation, KindCategory.ObjectCreation, 25);
        SetDistance(KindCategory.TypeOperation, KindCategory.Declaration, 40);
    }

    private static void SetDistance(KindCategory cat1, KindCategory cat2, byte distance)
    {
        var index = GetTriangularIndex((int)cat1, (int)cat2);
        categoryDistances[index] = distance;
    }

    private static int GetTriangularIndex(int cat1, int cat2)
    {
        // Ensure cat1 <= cat2 for triangular array indexing
        if (cat1 > cat2) (cat1, cat2) = (cat2, cat1);
        
        // Formula for upper triangular matrix index: cat1 * n - cat1 * (cat1 + 1) / 2 + cat2
        return cat1 * 40 - cat1 * (cat1 + 1) / 2 + cat2;
    }
}