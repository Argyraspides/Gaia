using System;
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class TerrainQuadTree
{
  private readonly ManualResetEventSlim CanPerformCulling = new(false);
  private Thread CullThread;

  private void StartCulling()
  {
    while (m_isRunning)
    {
      CanPerformCulling.Wait();
      try
      {
        foreach (TerrainQuadTreeNode rootNode in RootNodes)
        {
          if (!ExceedsMaxNodeThreshold())
          {
            break;
          }

          CullUnusedNodes(rootNode);
        }

        CanPerformCulling.Reset();
        CanPerformSearch.Set();
      }
      catch (Exception ex)
      {
        this.LogError($"Error in quadtree update thread: {ex}");
      }
    }
  }

  private void CullUnusedNodes(TerrainQuadTreeNode parentNode)
  {
    if (!GodotUtils.IsValid(parentNode))
    {
      return;
    }

    // We only want to cull nodes BELOW the ones that are currently visible in the scene
    if (parentNode.IsDeepestVisible)
    {
      RemoveSubQuadTreeThreadSafe(parentNode);
      return;
    }

    foreach (TerrainQuadTreeNode terrainQuadTreeNode in parentNode.ChildNodes)
    {
      CullUnusedNodes(terrainQuadTreeNode);
    }
  }

  private bool NodeVisibleToCamera(TerrainQuadTreeNode node) => false;

  private void RemoveQuadTreeNode(TerrainQuadTreeNode node)
  {
    if (GodotUtils.IsValid(node))
    {
      node.CallDeferred("queue_free");
    }
  }

  private bool ExceedsMaxNodeThreshold()
  {
    return GetTree().GetNodesInGroup(_nodeGroupName).Count >
           MaxNodes *
           MaxNodesCleanupThresholdPercent;
  }

  private void RemoveSubQuadTreeThreadSafe(TerrainQuadTreeNode parent)
  {
    if (!GodotUtils.IsValid(parent))
    {
      return;
    }

    foreach (TerrainQuadTreeNode childNode in parent.ChildNodes)
    {
      RemoveQuadTreeNode(childNode);
    }
  }
}
