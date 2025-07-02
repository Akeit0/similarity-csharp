using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp;

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
    public static double CalculateSimilarity(TreeNode tree1, TreeNode tree2, TsedOptions options)
    {
        var distance = AptedAlgorithm.ComputeEditDistance(tree1, tree2, options.AptedOptions);
        
        var size1 = (double)tree1.GetSubtreeSize();
        var size2 = (double)tree2.GetSubtreeSize();
        
        // TSED normalization: Use the larger tree size
        var maxSize = Math.Max(size1, size2);
        
        // Calculate base TSED similarity
        var tsedSimilarity = maxSize > 0 ? Math.Max(0, 1.0 - distance / maxSize) : 1.0;
        
        // Special handling when distance is 0 but sizes differ
        if (distance == 0 && Math.Abs(size1 - size2) > 0.001)
        {
            var sizeRatio = Math.Min(size1, size2) / Math.Max(size1, size2);
            var sizeDiff = Math.Abs(size1 - size2);
            
            if (sizeDiff > 10)
            {
                // Strong penalty for large absolute differences
                tsedSimilarity *= 0.5;
            }
            else if (sizeRatio < 0.95 || sizeDiff > 3)
            {
                // Moderate penalty for noticeable differences
                tsedSimilarity *= Math.Pow(sizeRatio, 0.5);
            }
        }
        
        // If trees are identical (distance = 0 and same size), don't apply structural penalties
        if (distance == 0 && Math.Abs(size1 - size2) < 0.001)
        {
            return Math.Max(0, Math.Min(1, tsedSimilarity));
        }
        
        // Apply size penalties for small trees
        if (options.SizePenalty)
        {
            if (maxSize < 10 && distance > 0)
            {
                tsedSimilarity *= 0.8; // Reduce similarity for small trees with differences
            }
            else if (maxSize < 30 && distance > 0)
            {
                tsedSimilarity *= 0.9; // Smaller penalty for moderately small trees
            }
        }
        
        // Apply additional structural penalties
        var similarity = tsedSimilarity;
        var sizeRatioPenalty = Math.Min(size1, size2) / Math.Max(size1, size2);
        
        if (options.SizePenalty)
        {
            var minSize = Math.Min(size1, size2);
            
            if (minSize < 30)
            {
                // Short function penalty
                var shortFunctionFactor = Math.Pow(minSize / 30.0, 0.5);
                similarity *= shortFunctionFactor;
                
                // Additional penalty for very short functions
                if (minSize < 10)
                {
                    similarity *= 0.5;
                }
            }
            
            // Apply size ratio penalty
            if (sizeRatioPenalty < 0.5)
            {
                similarity *= sizeRatioPenalty;
            }
            
            // Apply structural penalty to reduce false positives
            var structuralPenalty = CalculateStructuralPenalty(tree1, tree2, distance, maxSize);
            similarity *= structuralPenalty;
        }
        
        return Math.Max(0, Math.Min(1, similarity));
    }

    static double CalculateStructuralPenalty(TreeNode tree1, TreeNode tree2, double distance, double maxSize)
    {
        // Base penalty starts at 1.0 (no penalty)
        var penalty = 1.0;
        
        // Analyze structural differences
        var structure1 = AnalyzeStructure(tree1);
        var structure2 = AnalyzeStructure(tree2);
        
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
        var valueSimilarity = CalculateValueSimilarity(structure1, structure2);
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

    static StructuralFeatures AnalyzeStructure(TreeNode tree)
    {
        var features = new StructuralFeatures();
        AnalyzeNode(tree, features, 0);
        return features;
    }

    static double CalculateValueSimilarity(StructuralFeatures s1, StructuralFeatures s2)
    {
        // Calculate similarity of identifiers and literals used
        var allIdentifiers1 = s1.Identifiers.ToHashSet();
        var allIdentifiers2 = s2.Identifiers.ToHashSet();
        var allLiterals1 = s1.Literals.ToHashSet();
        var allLiterals2 = s2.Literals.ToHashSet();
        
        // Identifier similarity (property names, variable names, etc.)
        var identifierIntersection = allIdentifiers1.Intersect(allIdentifiers2).Count();
        var identifierUnion =  allIdentifiers1.Count + allIdentifiers2.Count - identifierIntersection;
        var identifierSimilarity = identifierUnion == 0 ? 1.0 : (double)identifierIntersection / identifierUnion;
        
        // Literal similarity (constant values, strings, numbers)
        var literalIntersection = allLiterals1.Intersect(allLiterals2).Count();
        var literalUnion = allLiterals1.Count + allLiterals2.Count - literalIntersection;
        var literalSimilarity = literalUnion == 0 ? 1.0 : (double)literalIntersection / literalUnion;
        
        // Weighted combination - identifiers more important for business logic
        return (identifierSimilarity * 0.7) + (literalSimilarity * 0.3);
    }

    static void AnalyzeNode(TreeNode node, StructuralFeatures features, int depth)
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
                features.LoopTypes.Add("for");
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.WhileStatement:
                features.LoopTypes.Add("while");
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.DoStatement:
                features.LoopTypes.Add("do");
                features.ControlFlowComplexity++;
                break;
            case SyntaxKind.ForEachStatement:
                features.LoopTypes.Add("foreach");
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

    class StructuralFeatures
    {
        public int ControlFlowComplexity { get; set; } = 0;
        public List<string> LoopTypes { get; set; } = new();
        public int ConditionalCount { get; set; } = 0;
        public int MethodCallCount { get; set; } = 0;
        public int VariableCount { get; set; } = 0;
        public int MaxNestingLevel { get; set; } = 0;
        public List<string> Identifiers { get; set; } = new();
        public List<string> Literals { get; set; } = new();
    }
}