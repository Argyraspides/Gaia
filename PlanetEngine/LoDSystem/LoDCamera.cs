using System;
using Gaia.Common.Utils.Logging;
using Gaia.PlanetEngine.Utils;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class LoDCamera : Camera3D
{

  public double[] AltitudeThresholds { private get; set; }

  private float _moveSpeed;
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
  }

  public override void _Process(double delta)
  {
    base._Process(delta);
    ProcessMoveAround((float)delta);
    AdjustSpeed();
    UpdateProperties();
  }

  private void UpdateProperties()
  {
    _altitude = GlobalPosition.Y;
  }

  private void ProcessMoveAround(float delta)
  {
    if (_altitude < _minAltitude)
    {
      GlobalPosition = new Vector3(GlobalPosition.X, _minAltitude + 1.0f, GlobalPosition.Z);
      return;
    }

    float visibleWidth = 2.0f * _altitude * Mathf.Atan(Mathf.DegToRad(Fov) / 2.0f);
    float _moveSpeed = visibleWidth / 2.0f;

    // Shift
    if (Input.IsActionPressed("ui_crouch"))
    {
      Transform = Transform.Translated(Vector3.Down * _moveSpeed * (float)delta);
    }

    // Space
    if (Input.IsActionPressed("ui_up"))
    {
      Transform = Transform.Translated(Vector3.Up * _moveSpeed * (float)delta);
    }

    // WASD
    if (Input.IsActionPressed("ui_forward"))
    {
      Vector3 newOffset = Vector3.Forward * _moveSpeed * (float)delta;
      Transform = Transform.Translated(newOffset);
    }

    if (Input.IsActionPressed("ui_backward"))
    {
      Vector3 newOffset = Vector3.Back * _moveSpeed * (float)delta;
      Transform = Transform.Translated(newOffset);
    }

    if (Input.IsActionPressed("ui_left"))
    {
      Vector3 newOffset = Vector3.Left * _moveSpeed * (float)delta;
      Transform = Transform.Translated(newOffset);
    }

    if (Input.IsActionPressed("ui_right"))
    {
      Vector3 newOffset = Vector3.Right * _moveSpeed * (float)delta;
      Transform = Transform.Translated(newOffset);
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

}

