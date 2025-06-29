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
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class TerrainQuadTree
{
  private const int THREAD_JOIN_TIMEOUT_MS = 1000;
  private readonly ManualResetEventSlim CanPerformSearch = new(false);

  private volatile bool m_isRunning;

  private Thread SplitOrMergeSearchThread;

  private void DetermineSplitOrMerge()
  {
    while (m_isRunning)
    {
      CanPerformSearch.Wait();
      try
      {
        foreach (TerrainQuadTreeNode rootNode in RootNodes)
        {
          DetermineSplitMergeNodes(rootNode, null);
        }

        _canUpdateQuadTree.Set();
        CanPerformSearch.Reset();
      }
      catch (Exception ex)
      {
        GD.PrintErr($"Error in quadtree update thread: {ex}");
      }
    }
  }

  private void DetermineSplitMergeNodes(TerrainQuadTreeNode node, TerrainQuadTreeNode parent)
  {
    if (!GodotUtils.IsValid(node))
    {
      return;
    }

    // Splitting happens top-down, so we do it first prior to recursing down further
    if (node.IsDeepestVisible && ShouldSplit(node))
    {
      SplitQueueNodes.Enqueue(node);
      return;
    }

    foreach (TerrainQuadTreeNode childNode in node.ChildNodes)
    {
      DetermineSplitMergeNodes(childNode, node);
    }

    // Merging happens bottom-up, so we do it after recursing down the tree
    if (ShouldMergeChildren(node))
    {
      MergeQueueNodes.Enqueue(node);
    }
  }

  // Should we split into four new nodes?
  private bool ShouldSplit(TerrainQuadTreeNode node)
  {
    if (!GodotUtils.IsValid(node))
    {
      return false;
    }

    if (node.Depth >= MaxDepth)
    {
      return false;
    }

    float distanceToCamera = node.GlobalPositionCpy.DistanceTo(CameraPosition);
    bool shouldSplit = SplitThresholds[node.Depth] > distanceToCamera;

    return shouldSplit;
  }

  // Should we merge back into our parent?
  private bool ShouldMerge(TerrainQuadTreeNode node)
  {
    if (!GodotUtils.IsValid(node))
    {
      return false;
    }

    if (node.Depth < MinDepth)
    {
      return false;
    }

    if (!node.IsDeepestVisible)
    {
      return false;
    }

    float distanceToCamera = node.GlobalPositionCpy.DistanceTo(CameraPosition);
    bool shouldMerge = MergeThresholds[node.Depth] < distanceToCamera;

    return shouldMerge;
  }

  private bool ShouldMergeChildren(TerrainQuadTreeNode parentNode)
  {
    if (!GodotUtils.IsValid(parentNode))
    {
      return false;
    }

    if (parentNode.Depth < MinDepth)
    {
      return false;
    }

    foreach (TerrainQuadTreeNode childNode in parentNode.ChildNodes)
    {
      if (!GodotUtils.IsValid(childNode) || !ShouldMerge(childNode))
      {
        return false;
      }
    }

    return true;
  }
}
