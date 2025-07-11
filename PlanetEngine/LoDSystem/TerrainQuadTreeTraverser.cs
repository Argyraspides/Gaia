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

[assembly: InternalsVisibleTo("TerrainQuadTree")]


namespace Gaia.PlanetEngine.LoDSystem;

/// <summary>
/// The purpose of this class is to run two intensive operations on two different threads to serve the
/// TerrainQuadTree class. These are:
/// - Culling nodes that are no longer needed when we exceed the max node threshold defined in the TerrainQuadTree class
/// - Determining if quadtree nodes need to be split/merged.
/// Both requiring the traversal of potentially the entire tree, thus these operations are delegated to this class
/// which spawns one thread for each operation.
///
///
/// </summary>
public partial class TerrainQuadTreeTraverser
{
    // True if we can perform the DFS search to determine which nodes should be culled, and cull them.
    // Accessible by the TerrainQuadTree.
    internal ManualResetEventSlim m_canPerformCulling = new ManualResetEventSlim(false);

    // True if we can perform the DFS search to determine which nodes should be split/merged
    private ManualResetEventSlim m_canPerformSearch = new ManualResetEventSlim(false);

    // Injected to us by TerrainQuadTree. We set this once we have culled all nodes and determined
    // which ones should be merged/split, so that the TerrainQuadTree can go ahead and split/merge them
    private ManualResetEventSlim m_canUpdateQuadTree;

    private readonly TerrainQuadTree m_terrainQuadTree;

    private Thread m_updateQuadTreeThread;
    private Thread m_cullQuadTreeThread;
    private volatile bool m_isRunning = false;

    private const int THREAD_JOIN_TIMEOUT_MS = 1000;

    public TerrainQuadTreeTraverser(TerrainQuadTree terrainQuadTree, ManualResetEventSlim canUpdateQuadTree)
    {
        m_terrainQuadTree = terrainQuadTree ?? throw new ArgumentNullException(nameof(terrainQuadTree));
        m_canUpdateQuadTree = canUpdateQuadTree ?? throw new ArgumentNullException(nameof(canUpdateQuadTree));
    }

    ~TerrainQuadTreeTraverser()
    {
        Stop();
    }

    public void Start()
    {
        m_updateQuadTreeThread = new Thread(DetermineSplitMergeNodeThreadFunction)
        {
            IsBackground = true, Name = "QuadTreeUpdateThread"
        };

        m_cullQuadTreeThread = new Thread(StartCullingThreadFunction)
        {
            IsBackground = true, Name = "CullQuadTreeThread"
        };

        m_updateQuadTreeThread.Start();
        m_cullQuadTreeThread.Start();
        m_canPerformSearch.Set();
        m_isRunning = true;
    }

    private void StartCullingThreadFunction()
    {
        while (m_isRunning)
        {
            m_canPerformCulling.Wait();
            try
            {
                if (m_terrainQuadTree.RootNodes == null) continue;

                foreach (var rootNode in m_terrainQuadTree.RootNodes)
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
        if (m_updateQuadTreeThread != null && m_updateQuadTreeThread.IsAlive)
        {
            m_updateQuadTreeThread.Join(THREAD_JOIN_TIMEOUT_MS);
        }

        if (m_cullQuadTreeThread != null && m_cullQuadTreeThread.IsAlive)
        {
            m_cullQuadTreeThread.Join(THREAD_JOIN_TIMEOUT_MS);
        }

        m_canPerformCulling.Dispose();
        m_canPerformSearch.Dispose();
    }

    private void DetermineSplitMergeNodeThreadFunction()
    {
        while (m_isRunning)
        {
            m_canPerformSearch.Wait();
            try
            {
                if (m_terrainQuadTree.RootNodes == null) continue;

                foreach (var rootNode in m_terrainQuadTree.RootNodes)
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
            m_terrainQuadTree.SplitQueueNodes.Enqueue(node);
            if (parent != null) m_terrainQuadTree.InvisibilityQueueNodes.Enqueue(parent);
            return;
        }

        foreach (var childNode in node.ChildNodes)
        {
            DetermineSplitMergeNodes(childNode, node);
        }

        // Merging happens bottom-up, so we do it after recursing down the tree
        if (ShouldMergeChildren(node))
        {
            m_terrainQuadTree.MergeQueueNodes.Enqueue(node);
            if (parent != null) m_terrainQuadTree.VisibilityQueueNodes.Enqueue(parent);
        }
    }

    private bool ExceedsMaxNodeThreshold()
    {
        return m_terrainQuadTree.CurrentNodeCount >
               m_terrainQuadTree.MaxNodes *
               m_terrainQuadTree.MaxNodesCleanupThresholdPercent;
    }

    // TODO::ARGYRASPIDES() { Make these not just distance based but also based on what is visible on the screen.
    // Sometimes the center of the screen is more detailed than the rest and it can look jarring if the map tiles
    // are from completely different times }
    private bool ShouldSplit(TerrainQuadTreeNode node)
    {
        if (!GodotUtils.IsValid(node)) throw new ArgumentNullException(nameof(node), "node cannot be null");
        if (node.Depth >= m_terrainQuadTree.MaxDepth) return false;

        float distanceToCamera = node.Position.DistanceTo(m_terrainQuadTree.CameraPosition);
        bool shouldSplit = m_terrainQuadTree.SplitThresholds[node.Depth] > distanceToCamera;

        return shouldSplit;
    }

    private bool ShouldMerge(TerrainQuadTreeNode node)
    {
        if (!GodotUtils.IsValid(node)) return false;
        if (node.Depth < m_terrainQuadTree.MinDepth) return false;

        float distanceToCamera = node.Position.DistanceTo(m_terrainQuadTree.CameraPosition);
        bool shouldMerge = m_terrainQuadTree.MergeThresholds[node.Depth] < distanceToCamera;

        return shouldMerge;
    }

    /// <summary>
    /// Checks if we should merge the children of the terrain quad tree node.
    /// In the LoD system, we only split as far as we need to, thus leaf nodes are
    /// the ones visible in the scene tree. We only merge the children back into the parent
    /// if ALL children are too far from the camera.
    /// </summary>
    /// <param name="parentNode">Parent node whose children will be tested for merging</param>
    /// <returns>True if the parents children should be merged, otherwise false</returns>
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

    /// <summary>
    /// Removes a quadtree node from the scene tree itself. The object will be deleted in the Godot world,
    /// but not in the C# world
    /// </summary>
    /// <param name="node"></param>
    private void RemoveQuadTreeNode(TerrainQuadTreeNode node)
    {
        if (GodotUtils.IsValid(node))
        {
            node.CallDeferred("queue_free");
        }
    }

    /// <summary>
    /// Completely removes the entire subtree of the parent node
    /// </summary>
    /// <param name="parent"></param>
    private void RemoveSubQuadTreeThreadSafe(TerrainQuadTreeNode parent)
    {
        if (!GodotUtils.IsValid(parent)) return;

        foreach (var childNode in parent.ChildNodes)
        {
            RemoveSubQuadTreeThreadSafe(childNode);
            RemoveQuadTreeNode(childNode);
        }
    }

    /// <summary>
    /// Culls any unused nodes in the scene tree. An unused node is any node which is not visible (hence not
    /// useful to the player) AND has no visible ancestors.
    /// </summary>
    /// <param name="parentNode">The parent node whose entire subtree will be culled</param>
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
