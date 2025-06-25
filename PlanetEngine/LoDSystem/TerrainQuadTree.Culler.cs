using System;
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class TerrainQuadTree
{
    
    private ManualResetEventSlim CanPerformCulling = new ManualResetEventSlim(false);
    private Thread CullThread;
    
    // True when we receive a notification that TerrainQuadTree is about to be deleted
    private bool DestructorActivated = false;
    
    private void StartCulling()
    {
        while (m_isRunning)
        {
            CanPerformCulling.Wait();
            try
            {

                foreach (var rootNode in RootNodes)
                {
                    if (!ExceedsMaxNodeThreshold()) break;
                    Logger.LogWarning($"Exceeded max node count! Culling now!");
                    CullUnusedNodes(rootNode);
                }

                CanPerformCulling.Reset();
                CanPerformSearch.Set();
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
            Logger.LogError("Found suitable nodes to cull! Culling now!");
            RemoveSubQuadTreeThreadSafe(parentNode);
            return;
        }

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
    
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            DestructorActivated = true;
        }
        // We don't want to attempt to GetTree().GetNodeCount() when the scene tree (or at the very least,
        // the TerrainQuadTree) is about to be deleted.
        else if (!DestructorActivated && what == NotificationChildOrderChanged)
        {
            CurrentNodeCount = GetTree().GetNodeCount();
            Logger.LogInfo($"Current node count: {CurrentNodeCount}");
        }
    }
}