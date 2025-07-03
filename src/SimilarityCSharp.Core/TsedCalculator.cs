using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp;

public enum LoopType
{
    For,
    While,
    Do,
    ForEach
}

public record TsedOptions
{
    public AptedOptions AptedOptions { get; set; } = new();
    public int MinLines { get; set; } = 5;
    public int MaxLines { get; set; } = int.MaxValue;
    public int MinTokens { get; set; } = 0;
    public bool SizePenalty { get; set; } = true;
    public Regex? IncludeMethodPattern { get; set; }
}

public static class TsedCalculator
{
    public static double CalculateSimilarity(MethodInfo method1, MethodInfo method2, TsedOptions options)
    {
        var tree1 = method1.Tree;
        var tree2 = method2.Tree;
        var distance = AptedAlgorithm.ComputeEditDistance(tree1, tree2, options.AptedOptions);

        var size1 = (double)tree1.GetSubtreeSize();
        var size2 = (double)tree2.GetSubtreeSize();

        // TSED normalization: Use the larger tree size
        var maxSize = Math.Max(size1, size2);
        var sizeRatio = Math.Min(size1, size2) / maxSize;


        // Calculate base TSED similarity
        var tsedSimilarity = maxSize > 0 ? Math.Max(0, 1.0 - distance / maxSize) : 1.0;

        var similarity = tsedSimilarity;

        // Apply line count penalty
        if (options.SizePenalty)
        {
            if (sizeRatio < 0.1)
            {
                similarity *= sizeRatio * 10; // Dramatic penalty for very different sizes
            }
            else if (sizeRatio < 0.3)
            {
                similarity *= (0.7 + sizeRatio); // Moderate penalty for moderately different sizes
            }

            var lineCount1 = method1.Lines;
            var lineCount2 = method2.Lines;
            var avgLineCount = (lineCount1 + lineCount2) / 2.0;
            if (avgLineCount < 10)
            {
                // For functions under 10 lines, apply a penalty based on line count
                var shortFunctionPenalty = avgLineCount / 10.0; // 0.5 for 5 lines, 0.8 for 8 lines, etc.
                similarity *= shortFunctionPenalty; // Reduce similarity based on line count
            }
        }

        var structuralPenalty = CalculateStructuralPenalty(method1, method2, distance, maxSize , options.AptedOptions.RenameCost);
        similarity *= structuralPenalty;
        return Math.Max(0, Math.Min(1, similarity));
    }

    static double CalculateStructuralPenalty(MethodInfo method1, MethodInfo method2, double distance, double maxSize,double renameCost)
    {
        // Base penalty starts at 1.0 (no penalty)
        var penalty = 1.0;

        // Analyze structural differences
        var structure1 = method1.StructuralFeatures;
        var structure2 = method2.StructuralFeatures;

        // Control flow complexity penalty - moderate approach
        var complexityDiff = Math.Abs(structure1.ControlFlowComplexity - structure2.ControlFlowComplexity);
        if (complexityDiff > 3)
        {
            penalty *= 0.8; // Penalty only for significantly different control flow
        }
        else if (complexityDiff > 1)
        {
            penalty *= 0.95; // Minor penalty for moderately different control flow
        }

        // Loop structure penalty - only for completely different patterns
        if (structure1.LoopTypes.Count > 0 && structure2.LoopTypes.Count > 0 &&
            !structure1.LoopTypes.SequenceEqual(structure2.LoopTypes))
        {
            penalty *= 0.9; // Moderate penalty for different loop patterns
        }

        // Conditional structure penalty
        var conditionalDiff = Math.Abs(structure1.ConditionalCount - structure2.ConditionalCount);
        if (conditionalDiff > 2)
        {
            penalty *= 0.85; // Penalty for significantly different conditional complexity
        }

        // Method call pattern penalty
        var maxCalls = Math.Max(structure1.MethodCallCount, structure2.MethodCallCount);
        var callDiff = Math.Abs(structure1.MethodCallCount - structure2.MethodCallCount);
        if (maxCalls > 0 && callDiff > maxCalls * 0.5)
        {
            penalty *= 0.9; // Penalty for very different call patterns
        }

        // Variable usage pattern penalty
        var maxVars = Math.Max(structure1.VariableCount, structure2.VariableCount);
        var varDiff = Math.Abs(structure1.VariableCount - structure2.VariableCount);
        if (maxVars > 0 && varDiff > maxVars * 0.4)
        {
            penalty *= 0.95; // Minor penalty for different variable usage
        }

        // Deep nesting penalty for different patterns
        var nestingDiff = Math.Abs(structure1.MaxNestingLevel - structure2.MaxNestingLevel);
        if (nestingDiff > 2)
        {
            penalty *= 0.9; // Penalty for significantly different nesting patterns
        }

        // High edit distance relative to size penalty
        var editRatio = distance / maxSize;
        if (editRatio > 0.4)
        {
            penalty *= Math.Pow(0.8, editRatio); // Exponential penalty for high edit ratios
        }

        // Value-based penalties for different business logic - conservative approach
        var valueSimilarity = CalculateValueSimilarity(structure1, structure2)*(1-renameCost);
        if (valueSimilarity < 0.3)
        {
            penalty *= 0.85; // Penalty only for very different value usage
        }
        else if (valueSimilarity < 0.5)
        {
            penalty *= 0.95; // Minor penalty for somewhat different values
        }

        return Math.Max(0.1, penalty); // Never reduce similarity below 10% of original
    }
    

    static double CalculateValueSimilarity(StructuralFeatures s1, StructuralFeatures s2)
    {
        // Calculate similarity of identifiers and literals used
        var allIdentifiers1 = s1.Identifiers;
        var allIdentifiers2 = s2.Identifiers;
        var allLiterals1 = s1.Literals;
        var allLiterals2 = s2.Literals;

        // Identifier similarity (property names, variable names, etc.)
        var identifierIntersection = allIdentifiers1.Intersect(allIdentifiers2).Count();
        var identifierUnion = allIdentifiers1.Count + allIdentifiers2.Count - identifierIntersection;
        var identifierSimilarity = identifierUnion == 0 ? 1.0 : (double)identifierIntersection / identifierUnion;

        // Literal similarity (constant values, strings, numbers)
        var literalIntersection = allLiterals1.Intersect(allLiterals2).Count();
        var literalUnion = allLiterals1.Count + allLiterals2.Count - literalIntersection;
        var literalSimilarity = literalUnion == 0 ? 1.0 : (double)literalIntersection / literalUnion;

        // Weighted combination - identifiers more important for business logic
        return (identifierSimilarity * 0.7) + (literalSimilarity * 0.3);
    }

   internal static void AnalyzeNode(TreeNode node, StructuralFeatures features, int depth)
    {
        features.MaxNestingLevel = Math.Max(features.MaxNestingLevel, depth);

        // Collect identifiers and literals for value-based analysis
        if (!string.IsNullOrEmpty(node.Value))
        {
            switch (node.Kind)
            {
                case SyntaxKind.IdentifierName:
                case SyntaxKind.SimpleMemberAccessExpression:
                case SyntaxKind.PointerMemberAccessExpression:
                    features.Identifiers.Add(node.Value);
                    break;
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                    features.Literals.Add(node.Value);
                    break;
            }
        }

        switch (node.Kind)
        {
            case SyntaxKind.ForStatement:
                features.LoopTypes.Add(LoopType.For);
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.WhileStatement:
                features.LoopTypes.Add(LoopType.While);
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.DoStatement:
                features.LoopTypes.Add(LoopType.Do);
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.ForEachStatement:
                features.LoopTypes.Add(LoopType.ForEach);
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.IfStatement:
                features.ConditionalCount++;
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.SwitchStatement:
                features.ConditionalCount++;
                features.ControlFlowComplexity += 2; // Switch is more complex
                break;
            case SyntaxKind.InvocationExpression:
                features.MethodCallCount++;
                break;
            case SyntaxKind.VariableDeclaration:
            case SyntaxKind.VariableDeclarator:
                features.VariableCount++;
                break;
            case SyntaxKind.TryStatement:
                features.ControlFlowComplexity += 2; // Exception handling is complex
                break;
        }

        foreach (var child in node.Children)
        {
            AnalyzeNode(child, features, depth + 1);
        }
    }

    internal class StructuralFeatures
    {
        public int ControlFlowComplexity { get; set; } = 0;
        public List<LoopType> LoopTypes { get; set; } = new();
        public int ConditionalCount { get; set; } = 0;
        public int MethodCallCount { get; set; } = 0;
        public int VariableCount { get; set; } = 0;
        public int MaxNestingLevel { get; set; } = 0;
        public HashSet<string> Identifiers { get; set; } = new();
        public HashSet<string> Literals { get; set; } = new();
    }
}