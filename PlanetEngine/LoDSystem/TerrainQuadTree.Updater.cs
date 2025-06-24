/*




     ,ad8888ba,         db         88         db
    d8"'    `"8b       d88b        88        d88b
   d8'                d8'`8b       88       d8'`8b
   88                d8'  `8b      88      d8'  `8b
   88      88888    d8YaaaaY8b     88     d8YaaaaY8b
   Y8,        88   d8""""""""8b    88    d8""""""""8b
    Y8a.    .a88  d8'        `8b   88   d8'        `8b
     `"Y88888P"  d8'          `8b  88  d8'          `8b

                     WEAVER OF WORLDS

*/

using System;
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Godot;


namespace Gaia.PlanetEngine.LoDSystem;

public partial class TerrainQuadTree
{

    private ManualResetEventSlim m_canPerformSearch = new ManualResetEventSlim(false);

    private Thread m_determineSplitOrMergeThread;
    
    private volatile bool m_isRunning = false;

    private const int THREAD_JOIN_TIMEOUT_MS = 1000;

    private void DetermineSplitOrMerge()
    {
        while (m_isRunning)
        {
            m_canPerformSearch.Wait();
            try
            {

                foreach (var rootNode in RootNodes)
                {
                    DetermineSplitMergeNodes(rootNode, null);
                }

                m_canUpdateQuadTree.Set();
                m_canPerformSearch.Reset();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Error in quadtree update thread: {ex}");
            }
        }
    }

    private void DetermineSplitMergeNodes(TerrainQuadTreeNode node, TerrainQuadTreeNode parent)
    {
        if (!GodotUtils.IsValid(node)) return;

        // Splitting happens top-down, so we do it first prior to recursing down further
        if (node.IsDeepestVisible && ShouldSplit(node))
        {
            SplitQueueNodes.Enqueue(node);
            return;
        }

        foreach (var childNode in node.ChildNodes)
        {
            DetermineSplitMergeNodes(childNode, node);
        }

        // Merging happens bottom-up, so we do it after recursing down the tree
        if (ShouldMergeChildren(node))
        {
            MergeQueueNodes.Enqueue(node);
        }
    }

    private int splitCt = 0;

    private bool ShouldSplit(TerrainQuadTreeNode node)
    {
        if (!GodotUtils.IsValid(node)) throw new ArgumentNullException(nameof(node), "node cannot be null");
        if (node.Depth >= MaxDepth) return false;
        
        float distanceToCamera = node.GlobalPositionCpy.DistanceTo(CameraPosition);
        bool shouldSplit = SplitThresholds[node.Depth] > distanceToCamera;
        
        return shouldSplit;
    }

    private bool ShouldMerge(TerrainQuadTreeNode node)
    {
        if (!GodotUtils.IsValid(node)) return false;
        if (node.Depth < MinDepth) return false;
        
        float distanceToCamera = node.GlobalPositionCpy.DistanceTo(CameraPosition);
        bool shouldMerge = MergeThresholds[node.Depth] < distanceToCamera;
        
        return shouldMerge;
    }

    private bool ShouldMergeChildren(TerrainQuadTreeNode parentNode)
    {
        if (!GodotUtils.IsValid(parentNode)) return false;
        if (parentNode.Depth < MinDepth) return false;

        foreach (var childNode in parentNode.ChildNodes)
        {
            if (!GodotUtils.IsValid(childNode) || !ShouldMerge(childNode))
            {
                return false;
            }
        }

        return true;
    }

}