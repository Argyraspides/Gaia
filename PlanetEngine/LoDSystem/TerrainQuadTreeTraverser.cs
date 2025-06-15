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
using System.Runtime.CompilerServices;
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Godot;


namespace Gaia.PlanetEngine.LoDSystem;

public partial class TerrainQuadTree
{
    private ManualResetEventSlim m_canPerformCulling = new ManualResetEventSlim(false);

    private ManualResetEventSlim m_canPerformSearch = new ManualResetEventSlim(false);

    private Thread m_determineSplitOrMergeThread;
    private Thread m_cullThread;
    
    private volatile bool m_isRunning = false;

    private const int THREAD_JOIN_TIMEOUT_MS = 1000;

    ~TerrainQuadTree()
    {
        Stop();
    }

    public void Start()
    {
        m_determineSplitOrMergeThread = new Thread(DetermineSplitOrMerge)
        {
            IsBackground = true, Name = "QuadTreeUpdateThread"
        };

        m_cullThread = new Thread(StartCulling)
        {
            IsBackground = true, Name = "CullQuadTreeThread"
        };

        m_determineSplitOrMergeThread.Start();
        m_cullThread.Start();
        m_canPerformSearch.Set();
        m_isRunning = true;
    }

    private void StartCulling()
    {
        while (m_isRunning)
        {
            m_canPerformCulling.Wait();
            try
            {

                foreach (var rootNode in RootNodes)
                {
                    if (!GodotUtils.IsValid(rootNode) || !ExceedsMaxNodeThreshold()) continue;
                    CullUnusedNodes(rootNode);
                }

                m_canPerformCulling.Reset();
                m_canPerformSearch.Set();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in quadtree update thread: {ex}");
            }
        }
    }

    public void Stop()
    {
        m_isRunning = false;
        if (m_determineSplitOrMergeThread != null && m_determineSplitOrMergeThread.IsAlive)
        {
            m_determineSplitOrMergeThread.Join(THREAD_JOIN_TIMEOUT_MS);
        }

        if (m_cullThread != null && m_cullThread.IsAlive)
        {
            m_cullThread.Join(THREAD_JOIN_TIMEOUT_MS);
        }

        m_canPerformCulling.Dispose();
        m_canPerformSearch.Dispose();
    }

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
        if (node.IsDeepest && ShouldSplit(node))
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

    private bool ExceedsMaxNodeThreshold()
    {
        return CurrentNodeCount >
               MaxNodes *
               MaxNodesCleanupThresholdPercent;
    }

    private bool ShouldSplit(TerrainQuadTreeNode node)
    {
        // if (!GodotUtils.IsValid(node)) throw new ArgumentNullException(nameof(node), "node cannot be null");
        // if (node.Depth >= MaxDepth) return false;
        //
        // float distanceToCamera = node.Position.DistanceTo(CameraPosition);
        // bool shouldSplit = SplitThresholds[node.Depth] > distanceToCamera;
        //
        // return shouldSplit;

        return false;
    }

    private bool ShouldMerge(TerrainQuadTreeNode node)
    {
        // if (!GodotUtils.IsValid(node)) return false;
        // if (node.Depth < MinDepth) return false;
        //
        // float distanceToCamera = node.Position.DistanceTo(CameraPosition);
        // bool shouldMerge = MergeThresholds[node.Depth] < distanceToCamera;
        //
        // return shouldMerge;

        return false;
    }

    private bool ShouldMergeChildren(TerrainQuadTreeNode parentNode)
    {
        if (!GodotUtils.IsValid(parentNode)) return false;

        foreach (var childNode in parentNode.ChildNodes)
        {
            if (!GodotUtils.IsValid(childNode) || !ShouldMerge(childNode))
            {
                return false;
            }
        }

        return true;
    }

    private void RemoveQuadTreeNode(TerrainQuadTreeNode node)
    {
        if (GodotUtils.IsValid(node))
        {
            node.CallDeferred("queue_free");
        }
    }

    private void RemoveSubQuadTreeThreadSafe(TerrainQuadTreeNode parent)
    {
        if (!GodotUtils.IsValid(parent)) return;

        foreach (var childNode in parent.ChildNodes)
        {
            RemoveSubQuadTreeThreadSafe(childNode);
            RemoveQuadTreeNode(childNode);
        }
    }

    private void CullUnusedNodes(TerrainQuadTreeNode parentNode)
    {
        if (!GodotUtils.IsValid(parentNode)) return;

        // We only want to cull nodes BELOW the ones that are currently visible in the scene
        if (parentNode.IsDeepest)
        {
            // Cull all sub-trees below the parent
            RemoveSubQuadTreeThreadSafe(parentNode);
            return;
        }

        // Recursively destroy all nodes
        foreach (var terrainQuadTreeNode in parentNode.ChildNodes)
        {
            if (GodotUtils.IsValid(terrainQuadTreeNode))
            {
                CullUnusedNodes(terrainQuadTreeNode);
            }
        }
    }
}