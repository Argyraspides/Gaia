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
     _maxAltitude = (float)_altitudeThresholds.First();
    }
  }

  private float _moveSpeed;
  [Export] private float _scrollMoveSpeedFactor = 0.1f;
  private float _altitude = float.MaxValue;

  [Export] private float _maxAltitude = 30_000.0f;
  [Export] private float _minAltitude = 0.20f;
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
    HandleMouseDragControl(@event);
    HandleMouseZoomControl(@event);
  }

  /// <summary>
  /// Handles LoD camera dragging controls. The intended effect is that the mouse cursor stays over the exact same lat/lon
  /// position on the planet as you drag around ... though this function assumes you're always looking top-down (and that we
  /// are staring at a flat arrangement).
  /// Will definitely have to change when we introduce 3D (globe earth and stuff)
  /// </summary>
  /// <param name="event"></param>
  private void HandleMouseDragControl(InputEvent @event)
  {
    if (@event is InputEventMouseButton mouseButton)
    {
      _isDragging = mouseButton.Pressed;
      _lastMousePos = mouseButton.Position;
      return;
    }

    if (@event is InputEventMouseMotion mouseMotion && _isDragging)
    {
        Vector2 mouseMoveDelta = _lastMousePos - mouseMotion.Position;
        _lastMousePos = mouseMotion.Position;

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

        float verticalFov = Mathf.DegToRad(Fov);
        float tanVertHalfFov = Mathf.Tan(verticalFov / 2);

        float horizontalFov = 2 * Mathf.Atan(tanVertHalfFov * (viewportSize.X / viewportSize.Y));

        float realXCooPerPixel = 2 * _altitude * (Mathf.Tan(horizontalFov / 2) / viewportSize.X);
        float realYCooPerPixel = 2 * _altitude * (tanVertHalfFov / viewportSize.Y);

        float xCooMove = mouseMoveDelta.X * realXCooPerPixel;
        float yCooMove = mouseMoveDelta.Y * realYCooPerPixel;

        Position = new Vector3(Position.X + xCooMove, Position.Y, Position.Z + yCooMove);
    }
  }

  private void HandleMouseZoomControl(InputEvent @event)
  {
    if (@event is not InputEventMouseButton mouseButton)
    {
      return;
    }

    switch( mouseButton.ButtonIndex )
    {
      case MouseButton.WheelUp:
        Transform = Transform.Translated(Vector3.Down* _moveSpeed * _scrollMoveSpeedFactor);
        break;
      case MouseButton.WheelDown:
        Transform = Transform.Translated(Vector3.Up * _moveSpeed * _scrollMoveSpeedFactor);
        break;
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

