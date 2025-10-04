using System;
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class TerrainQuadTree
{
  private readonly ManualResetEventSlim _canPerformCulling = new(false);
  private Thread _cullThread;

  private void StartCulling()
  {
    while (_isRunning)
    {
      _canPerformCulling.Wait();
      try
      {
        foreach (TerrainQuadTreeNode rootNode in _rootNodes)
        {
          if (!ExceedsMaxNodeThreshold())
          {
            break;
          }

          CullUnusedNodes(rootNode);
        }

        _canPerformCulling.Reset();
        _canPerformSearch.Set();
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
           _maxNodes *
           _maxNodesCleanupThresholdPercent;
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
