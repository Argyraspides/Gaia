using System;
using Gaia.PlanetEngine.Utils;
using Godot;

namespace Gaia.SolarSystem.Scenes.SolarSystem;

public partial class LoDCamera : Camera3D
{
  public int MaxVisibleDepth { get; set; }
  public double[] AltitudeThresholds { get; set; }

  private float _moveSpeed;
  public float Altitude { get; set; } = float.MaxValue;
  private float _maxAltitude = float.MaxValue;
  private float _minAltitude;

  private float _pitch;
  private float _pitchSpeed = 0.001f;

  private float _yaw;
  private float _yawSpeed = 0.001f;


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
    AdjustSpeed((float)delta);
    // ProcessLookAround();
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
    // WASD
    if (Input.IsActionPressed("ui_forward"))
    {
      Transform = Transform.Translated(Vector3.Forward * _moveSpeed * delta);
    }

    if (Input.IsActionPressed("ui_backward"))
    {
      Transform = Transform.Translated(Vector3.Back * _moveSpeed * delta);
    }

    if (Input.IsActionPressed("ui_left"))
    {
      Transform = Transform.Translated(Vector3.Left * _moveSpeed * delta);
    }

    if (Input.IsActionPressed("ui_right"))
    {
      Transform = Transform.Translated(Vector3.Right * _moveSpeed * delta);
    }

    // Shift
    if (Input.IsActionPressed("ui_crouch"))
    {
      Transform = Transform.Translated(Vector3.Down * _moveSpeed * delta);
    }

    // Space
    if (Input.IsActionPressed("ui_up"))
    {
      Transform = Transform.Translated(Vector3.Up * _moveSpeed * delta);
    }
  }

  private void AdjustSpeed(float delta)
  {
    float tileWidth = PlanetUtils.EarthEquatorialCircumferenceKm / (1 << MaxVisibleDepth);
    float maxVisibleRange = 2.0f * Altitude * Mathf.Atan(Fov / 2.0f);

    // Max no. of tiles we can see on-screen
    float tileSpan = maxVisibleRange / tileWidth;

    // 2 tiles per second
    _moveSpeed = (2 / tileSpan) * tileWidth;
  }

}

