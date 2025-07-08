using System;
using Gaia.Common.Utils.Logging;
using Gaia.PlanetEngine.Utils;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class LoDCamera : Camera3D
{

  public double[] AltitudeThresholds { private get; set; }

  private float _moveSpeed;
  private float _groundRef;
  public float _altitude = float.MaxValue;
  private float _maxAltitude = float.MaxValue;
  private float _minAltitude = 0.0f;
  private int _currentDepth = 0;

  private float _pitch;
  private float _pitchSpeed = 0.001f;

  private float _yaw;
  private float _yawSpeed = 0.001f;

  public override void _Ready()
  {
    this.RegisterLogging(true);
  }


  public override void _Input(InputEvent @event)
  {
    // base._Input(@event);
    // if (@event is InputEventMouseMotion mouseMotion)
    // {
    //     _yaw -= mouseMotion.Relative.X * _yawSpeed;
    //     _pitch -= mouseMotion.Relative.Y * _pitchSpeed;
    // }
  }

  public override void _Process(double delta)
  {
    base._Process(delta);
    ProcessMoveAround((float)delta);
    AdjustSpeed();
    UpdateProperties();
    // ProcessLookAround();
  }

  private void UpdateProperties()
  {
    _altitude = GlobalPosition.Y - _groundRef;
  }

  private void ProcessLookAround()
  {
    Transform3D transform = Transform;
    transform.Basis = Basis.Identity;
    Transform = transform;

    RotateObjectLocal(Vector3.Up, _yaw);
    RotateObjectLocal(Vector3.Right, _pitch);
  }

  private void ProcessMoveAround(float delta)
  {
    if (_altitude < _minAltitude)
    {
      GlobalPosition = new Vector3(GlobalPosition.X, _minAltitude + 1.0f, GlobalPosition.Z);
      return;
    }
  }

  private void AdjustSpeed()
  {

    // TODO::GAUGAMELA bruh i dont wanna be doing this O(20) shit every frame
    for (int i = 0; i < AltitudeThresholds.Length - 1; i++)
    {
      if (_altitude < AltitudeThresholds[i] && _altitude > AltitudeThresholds[i + 1])
      {
        _currentDepth = i;
      }
    }

    float visibleWidth = 2.0f * _altitude * Mathf.Atan(Mathf.DegToRad(Fov) / 2.0f);
    _moveSpeed = visibleWidth / 2.0f;
  }

  public void UpdateGroundRef(float y)
  {
    _groundRef = y;
  }

}

