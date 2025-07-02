using System.Collections.Concurrent;

namespace SimilarityCSharp;

public record AptedOptions
{
    public double RenameCost { get; set; } = 0.3;
    public double DeleteCost { get; set; } = 1.0;
    public double InsertCost { get; set; } = 1.0;
    public double KindDistanceWeight { get; set; } = 0.5;
}

public static class AptedAlgorithm
{
    static readonly ConcurrentQueue<Dictionary<(int, int), double>> memoPool = new();
    
    public static double ComputeEditDistance(TreeNode tree1, TreeNode tree2, AptedOptions options)
    {
        // Get or create memoization table from pool
        if (!memoPool.TryDequeue(out var memo))
        {
            memo = new();
        }
        
        // Estimate allocator size based on tree sizes
        var maxTreeSize = Math.Max(tree1.GetSubtreeSize(), tree2.GetSubtreeSize());
        var allocatorSize = maxTreeSize * 4; // Conservative estimate for all allocations
        
        var allocator = new LinearAllocator<double>(allocatorSize);
        
        try
        {
            return ComputeDistance(tree1, tree2, options, memo, ref allocator);
        }
        finally
        {
            // Clear and return to pool
            memo.Clear();
            memoPool.Enqueue(memo);
            allocator.Dispose();
        }
    }

    static double ComputeDistance(TreeNode node1, TreeNode node2, AptedOptions options, 
        Dictionary<(int, int), double> memo, ref LinearAllocator<double> allocator)
    {
        var key = (node1.Id, node2.Id);
        if (memo.TryGetValue(key, out var cached))
            return cached;

        double cost;
        
        if (node1.IsLeaf && node2.IsLeaf)
        {
            // Both are leaves - just compare them
            cost = GetRenameCost(node1, node2, options);
        }
        else if (node1.IsLeaf)
        {
            // node1 is leaf, node2 has children
            // Cost is deleting node2's subtree and renaming
            cost = options.DeleteCost * node2.GetSubtreeSize() - options.DeleteCost + 
                   GetRenameCost(node1, node2, options);
        }
        else if (node2.IsLeaf)
        {
            // node2 is leaf, node1 has children
            // Cost is inserting node1's subtree and renaming
            cost = options.InsertCost * node1.GetSubtreeSize() - options.InsertCost + 
                   GetRenameCost(node1, node2, options);
        }
        else
        {
            // Both have children - compute optimal alignment
            var renameCost = GetRenameCost(node1, node2, options);
            var childrenCost = ComputeChildrenDistance(node1.Children, node2.Children, options, memo,ref  allocator);
            cost = renameCost + childrenCost;
        }

        memo[key] = cost;
        return cost;
    }

    static double GetRenameCost(TreeNode node1, TreeNode node2, AptedOptions options)
    {
        double baseCost = 0;
        
        if (node1.Kind != node2.Kind)
        {
            var kindDistance = KindDistance.CalculateDistance(node1.Kind, node2.Kind);
            baseCost =  (1 + kindDistance * options.KindDistanceWeight);
        }
        else if (options.RenameCost!=0 && node1.Value != node2.Value)
        {
            baseCost = options.RenameCost; // Small cost for same kind but different value
        }
            
        return baseCost;
    }

    static double ComputeChildrenDistance(List<TreeNode> children1, List<TreeNode> children2, 
        AptedOptions options, Dictionary<(int, int), double> memo, ref LinearAllocator<double> allocator)
    {
        var m = children1.Count;
        var n = children2.Count;
        
        if (m == 0)
            return n * options.InsertCost;
        if (n == 0)
            return m * options.DeleteCost;

        // Ensure n <= m for space optimization (use smaller dimension as columns)
        var deleteCost = options.DeleteCost;
        var insertCost = options.InsertCost;
        
        if (n > m)
        {
            // Swap to make n the smaller dimension
            (children1, children2) = (children2, children1);
            (m, n) = (n, m);
            (deleteCost, insertCost) = (insertCost, deleteCost);
        }

        // Use LinearAllocator for rows
        var prevRow = allocator.Allocate(n + 1);
        var currRow = allocator.Allocate(n + 1);
        
        // Initialize base case (first row)
        for (int j = 0; j <= n; j++)
            prevRow[j] = j * insertCost;

        // Fill row by row
        for (int i = 1; i <= m; i++)
        {
            currRow[0] = i * deleteCost; // Base case for column 0
            
            for (int j = 1; j <= n; j++)
            {
                var deleteNodeCost = prevRow[j] + deleteCost * children1[i - 1].GetSubtreeSize();
                var insertNodeCost = currRow[j - 1] + insertCost * children2[j - 1].GetSubtreeSize();
                var replaceCost = prevRow[j - 1] + ComputeDistance(children1[i - 1], children2[j - 1], options, memo, ref allocator);
                
                currRow[j] = Math.Min(Math.Min(deleteNodeCost, insertNodeCost), replaceCost);
            }
            
            // Swap rows for next iteration
             var temp = currRow;
            currRow = prevRow;
            prevRow = temp;
        }

        var result = prevRow[n];
        allocator.Deallocate(2 * (n + 1)); // Deallocate both rows
        return result; // Result is in prevRow after final swap
    }
}