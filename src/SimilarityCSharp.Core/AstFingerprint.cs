using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.CSharp;

namespace SimilarityCSharp;

public readonly struct BloomFilter128
{
    readonly UInt128 bits;

    public BloomFilter128(UInt128 bits)
    {
        this.bits = bits;
    }

    public static BloomFilter128 Empty => new(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BloomFilter128 SetBit(int bitIndex)
    {
        // Simple and efficient bit setting using UInt128
        bitIndex &= 0x7F; // Ensure bitIndex is in range 0-127
        return new(bits | (UInt128.One << bitIndex));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BloomFilter128 Union(BloomFilter128 other)
    {
        return new(bits | other.bits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BloomFilter128 Intersect(BloomFilter128 other)
    {
        return new(bits & other.bits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
    {
        // UInt128 doesn't have PopCount, so split into two 64-bit parts
        var lower = (ulong)bits;
        var upper = (ulong)(bits >> 64);
        return BitOperations.PopCount(lower) + BitOperations.PopCount(upper);
    }

    public bool IsEmpty => bits == 0;

    public UInt128 Bits => bits;
}

public class AstFingerprint
{
    BloomFilter128 bloomFilter;
    readonly Dictionary<SyntaxKind, int> nodeTypeCounts;

    public AstFingerprint(TreeNode root)
    {
        
        bloomFilter = BloomFilter128.Empty;
        nodeTypeCounts = new();
        CountNodes(root);
    }

    int CountNodes(TreeNode node)
    {
        // Count this node
        if (!nodeTypeCounts.TryAdd(node.Kind, 1))
            nodeTypeCounts[node.Kind]++;

        // Add to bloom filter

        if (!string.IsNullOrEmpty(node.Value))
            AddToBloomFilter(node.Value);
        else AddToBloomFilter(node.Kind);
        // Count children
        int count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }

    void AddToBloomFilter(SyntaxKind kind)
    {
        // Hash the SyntaxKind enum value directly
        var kindValue = (uint)kind;
        var hash1 = SimpleHashUInt(kindValue, 31);

        // Set 3 bits in the bloom filter (128-bit filter)
        bloomFilter = bloomFilter.SetBit((int)(hash1 % 128));
    }

    void AddToBloomFilter(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        // Use simple hash functions matching Rust implementation
        var hash1 = SimpleHash(value, 31);
        var hash2 = SimpleHash(value, 37);
        var hash3 = SimpleHash(value, 41);

        // Set 3 bits in the bloom filter (128-bit filter)
        bloomFilter = bloomFilter.SetBit((int)(hash1 % 128));
        bloomFilter = bloomFilter.SetBit((int)(hash2 % 128));
        bloomFilter = bloomFilter.SetBit((int)(hash3 % 128));
    }

    static ulong SimpleHashUInt(uint value, ulong multiplier)
    {
        // Simple hash for uint values
        return unchecked(value * multiplier + 0x9e3779b9);
    }

    static ulong SimpleHash(string s, ulong multiplier)
    {
        ulong hash = 0;
        foreach (char c in s)
        {
            hash = unchecked(hash * multiplier + c);
        }

        return hash;
    }

    public bool MightBeSimilar(AstFingerprint other, double threshold = 0.3)
    {
        //return true;
        // Very lenient check - matches Rust implementation behavior
        var selfPopCount = bloomFilter.PopCount();
        var otherPopCount = other.bloomFilter.PopCount();

        // If either has no bits set, allow comparison
        if (selfPopCount == 0 || otherPopCount == 0) return true;

        var intersection = bloomFilter.Intersect(other.bloomFilter);
        var overlap = intersection.PopCount();

        var similarity = (double)overlap / Math.Max(selfPopCount, otherPopCount);
        if (similarity > threshold) return true;

        // Only reject if there's absolutely no overlap
        return false;
    }

    public double CalculateSimilarity(AstFingerprint other)
    {
        // Calculate weighted node type similarity - matches Rust implementation
        double totalDiff = 0;
        double totalWeight = 0;

        // Get all node types
        var allTypes = new HashSet<SyntaxKind>(nodeTypeCounts.Keys);
        foreach (var type in other.nodeTypeCounts.Keys)
        {
            allTypes.Add(type);
        }

        foreach (var nodeType in allTypes)
        {
            ref var count1Ref = ref CollectionsMarshal.GetValueRefOrNullRef(nodeTypeCounts, nodeType);
            ref var count2Ref = ref CollectionsMarshal.GetValueRefOrNullRef(other.nodeTypeCounts, nodeType);
            double count1 = Unsafe.IsNullRef(ref count1Ref) ? 0 : count1Ref;
            double count2 = Unsafe.IsNullRef(ref count2Ref) ? 0 : count2Ref;

            // Weight by importance of node type
            double weight = GetNodeWeight(nodeType);

            if (count1 > 0 || count2 > 0)
            {
                double maxCount = Math.Max(count1, count2);
                double diff = Math.Abs(count1 - count2) / maxCount;
                totalDiff += diff * weight;
                totalWeight += weight;
            }
        }

        if (totalWeight == 0) return 1.0;

        return 1.0 - (totalDiff / totalWeight);
    }

    static double GetNodeWeight(SyntaxKind nodeType)
    {
        // Match Rust implementation weights using SyntaxKind enum
        return nodeType switch
        {
            // Control flow is very important
            SyntaxKind.IfStatement or SyntaxKind.ForStatement or SyntaxKind.WhileStatement or
                SyntaxKind.DoStatement or SyntaxKind.ForEachStatement => 2.0,
            SyntaxKind.SwitchStatement or SyntaxKind.ConditionalExpression => 1.8,

            // Function-related nodes
            SyntaxKind.MethodDeclaration or SyntaxKind.ConstructorDeclaration or SyntaxKind.LocalFunctionStatement => 1.5,
            SyntaxKind.InvocationExpression or SyntaxKind.ObjectCreationExpression => 1.3,

            // Error handling
            SyntaxKind.TryStatement or SyntaxKind.ThrowStatement => 1.5,

            // Binary operations
            SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or
                SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression => 1.2,
            SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or
                SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression => 1.1,

            // Other important expressions
            SyntaxKind.SimpleAssignmentExpression or SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression => 1.0,
            SyntaxKind.ElementAccessExpression or SyntaxKind.ArrayCreationExpression => 0.9,

            // Variable declarations
            SyntaxKind.VariableDeclaration or SyntaxKind.VariableDeclarator => 0.8,

            // Literals and identifiers
            SyntaxKind.IdentifierName or SyntaxKind.StringLiteralExpression or
                SyntaxKind.NumericLiteralExpression or SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => 0.5,

            // Other nodes
            _ => 0.3
        };
    }
}