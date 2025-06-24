using System;
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class TerrainQuadTree
{
    
    private ManualResetEventSlim m_canPerformCulling = new ManualResetEventSlim(false);
    private Thread m_cullThread;
    
    private void StartCulling()
    {
        while (m_isRunning)
        {
            m_canPerformCulling.Wait();
            try
            {

                foreach (var rootNode in RootNodes)
                {
                    if (!ExceedsMaxNodeThreshold()) continue;
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
    
    private void CullUnusedNodes(TerrainQuadTreeNode parentNode)
    {
        if (!GodotUtils.IsValid(parentNode)) return;

        // We only want to cull nodes BELOW the ones that are currently visible in the scene
        if (parentNode.IsDeepestVisible || !NodeVisibleToCamera(parentNode))
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

    private bool NodeVisibleToCamera(TerrainQuadTreeNode node)
    {
        return false;
    }
    
    private void RemoveQuadTreeNode(TerrainQuadTreeNode node)
    {
        if (GodotUtils.IsValid(node))
        {
            node.CallDeferred("queue_free");
        }
    }
    
    private bool ExceedsMaxNodeThreshold()
    {
        return CurrentNodeCount >
               MaxNodes *
               MaxNodesCleanupThresholdPercent;
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
    
}