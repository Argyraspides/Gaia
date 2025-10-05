using System;
using System.Linq;
using Gaia.PlanetEngine.Utils;
using Godot;
using Daedalus.Logging;

namespace Gaia.PlanetEngine.LoDSystem;

public partial class LoDCamera : Camera3D
{

  private double[] _altitudeThresholds;
  public double[] AltitudeThresholds
  {
    set
    {
     _altitudeThresholds = value;
     _minAltitude = (float)_altitudeThresholds.Last();
    }
  }

  private float _moveSpeed;
  private float _altitude = float.MaxValue;
  // TODO::ARGYRASPIDES() { These are hardcoded, later on just make them tied to the actual zoom level }
  private float _maxAltitude = 30_000.0f;
  private float _minAltitude = 0.20f;
  private int _currentDepth;

  private float _pitch;
  private float _pitchSpeed = 0.001f;

  private float _yaw;
  private float _yawSpeed = 0.001f;

  private bool _isDragging;
  private Vector2 _lastMousePos;

  public override void _Ready()
  {
    Logger.RegisterLogging(this, true);
  }

  public override void _Process(double delta)
  {
    base._Process(delta);
    ProcessMoveAround((float)delta);
    AdjustSpeed();
    UpdateProperties();
  }

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventMouseButton mouseButton)
    {
      _isDragging = mouseButton.Pressed;
      _lastMousePos = mouseButton.Position;
      return;
    }

    if (@event is InputEventMouseMotion mouseMotion)
    {
      if (_isDragging)
      {
        Vector2 deltaPos = _lastMousePos - mouseMotion.Position;
        _lastMousePos = mouseMotion.Position;
        float verticalFov = Mathf.DegToRad(Fov);
        float horizontalFov = 2 * Mathf.Atan(Mathf.Tan(verticalFov / 2) * ( GetViewport().GetVisibleRect().Size.X / GetViewport().GetVisibleRect().Size.Y ));
        float realXCooPerPixel = 2 * _altitude * (Mathf.Tan(horizontalFov / 2) / GetViewport().GetVisibleRect().Size.X);
        float realYCooPerPixel = 2 * _altitude * (Mathf.Tan(verticalFov / 2) / GetViewport().GetVisibleRect().Size.Y);
        float xCooMove = deltaPos.X * realXCooPerPixel;
        float yCooMove = deltaPos.Y * realYCooPerPixel;
        Position = new Vector3(Position.X + xCooMove, Position.Y, Position.Z + yCooMove);
      }
    }
  }

  private void UpdateProperties()
  {
    _altitude = GlobalPosition.Y;
  }

  private void ProcessMoveAround(float delta)
  {
    if (_altitude < _minAltitude)
    {
      GlobalPosition = new Vector3(GlobalPosition.X, _minAltitude, GlobalPosition.Z);
      return;
    }

    if (_altitude > _maxAltitude)
    {
      GlobalPosition = new Vector3(GlobalPosition.X, _maxAltitude, GlobalPosition.Z);
      return;
    }

    float visibleWidth = 2.0f * _altitude * Mathf.Atan(Mathf.DegToRad(Fov) / 2.0f);
    float _moveSpeed = visibleWidth / 2.0f;

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

    // WASD
    if (Input.IsActionPressed("ui_forward"))
    {
      Vector3 newOffset = Vector3.Forward * _moveSpeed * delta;
      Transform = Transform.Translated(newOffset);
    }

    if (Input.IsActionPressed("ui_backward"))
    {
      Vector3 newOffset = Vector3.Back * _moveSpeed * delta;
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
    // TODO::ARGYRASPIDES bruh i dont wanna be doing this O(20) shit every frame
    for (int i = 0; i < _altitudeThresholds.Length - 1; i++)
    {
      if (_altitude < _altitudeThresholds[i] && _altitude > _altitudeThresholds[i + 1])
      {
        _currentDepth = i;
      }
    }

    float visibleWidth = 2.0f * _altitude * Mathf.Atan(Mathf.DegToRad(Fov) / 2.0f);
    _moveSpeed = visibleWidth / 2.0f;
  }

}

