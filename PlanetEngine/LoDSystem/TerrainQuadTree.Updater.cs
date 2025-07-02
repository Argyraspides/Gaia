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
// TODO:: lod camera should be owned by LOD system.
public partial class TerrainQuadTree
{
  private const int _threadJoinTimeoutMs = 1000;
  private readonly ManualResetEventSlim _canPerformSearch = new(false);

  private volatile bool _isRunning;

  private Thread _splitOrMergeSearchThread;

  private void DetermineSplitOrMerge()
  {
    while (_isRunning)
    {
      _canPerformSearch.Wait();
      try
      {
        foreach (TerrainQuadTreeNode rootNode in _rootNodes)
        {
          DetermineSplitMergeNodes(rootNode, null);
        }

        _canUpdateQuadTree.Set();
        _canPerformSearch.Reset();
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
      _splitQueueNodes.Enqueue(node);
      return;
    }

    foreach (TerrainQuadTreeNode childNode in node.ChildNodes)
    {
      DetermineSplitMergeNodes(childNode, node);
    }

    // Merging happens bottom-up, so we do it after recursing down the tree
    if (ShouldMergeChildren(node))
    {
      _mergeQueueNodes.Enqueue(node);
    }
  }

  // Should we split into four new nodes?
  private bool ShouldSplit(TerrainQuadTreeNode node)
  {
    if (!GodotUtils.IsValid(node))
    {
      return false;
    }

    if (node.Depth >= _maxDepth)
    {
      return false;
    }

    float distanceToCamera = node.GlobalPositionCpy.DistanceTo(_cameraPosition);
    bool shouldSplit = _splitThresholds[node.Depth] > distanceToCamera;

    // TODO:: should this be done here??? Awkward spot to update camera info ...
    if (shouldSplit)
    {
      _lodCamera.MaxVisibleDepth = Math.Max(node.Depth + 1, _lodCamera.MaxVisibleDepth);
    }

    return shouldSplit;
  }

  // Should we merge back into our parent?
  private bool ShouldMerge(TerrainQuadTreeNode node)
  {
    if (!GodotUtils.IsValid(node))
    {
      return false;
    }

    if (node.Depth < _minDepth)
    {
      return false;
    }

    if (!node.IsDeepestVisible)
    {
      return false;
    }

    float distanceToCamera = node.GlobalPositionCpy.DistanceTo(_cameraPosition);
    bool shouldMerge = _mergeThresholds[node.Depth] < distanceToCamera;

    // TODO:: should this be done here??? Awkward spot to update camera info ...
    if (shouldMerge)
    {
      _lodCamera.MaxVisibleDepth = Math.Max(node.Depth - 1, _lodCamera.MaxVisibleDepth);
      // TODO:: This is NOT accurate altitude. Fix later when you introduce raycasting!
      // We could be merging a node that is NOT directly underneat the camera thus we get
      // an altitude at a weird angle to the ground rather than perpendicular.
    }

    return shouldMerge;
  }

  private bool ShouldMergeChildren(TerrainQuadTreeNode parentNode)
  {
    if (!GodotUtils.IsValid(parentNode))
    {
      return false;
    }

    if (parentNode.Depth < _minDepth)
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
