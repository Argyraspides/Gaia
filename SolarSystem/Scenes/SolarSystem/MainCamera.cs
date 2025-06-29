using Godot;

public partial class MainCamera : Camera3D
{
  private float _pitch;
  private float _pitchSpeed = 0.001f;

  private float _yaw;
  private float _yawSpeed = 0.001f;

  [Export] public float MoveSpeed = 5.0f;

  public override void _Input(InputEvent @event)
  {
    // base._Input(@event);
    // if (@event is InputEventMouseMotion mouseMotion)
    // {
    //     m_yaw -= mouseMotion.Relative.X * m_yawSpeed;
    //     m_pitch -= mouseMotion.Relative.Y * m_pitchSpeed;
    // }
  }

  public override void _Process(double delta)
  {
    base._Process(delta);
    ProcessMoveAround();
    ProcessLookAround();
  }

  private void ProcessLookAround()
  {
    // Transform3D transform = Transform;
    // transform.Basis = Basis.Identity;
    // Transform = transform;

    RotateObjectLocal(Vector3.Up, _yaw);
    RotateObjectLocal(Vector3.Right, _pitch);
  }

  private void ProcessMoveAround()
  {
    // WASD
    if (Input.IsActionPressed("ui_forward"))
    {
      Transform = Transform.Translated(Vector3.Forward * MoveSpeed);
    }

    if (Input.IsActionPressed("ui_backward"))
    {
      Transform = Transform.Translated(Vector3.Back * MoveSpeed);
    }

    if (Input.IsActionPressed("ui_left"))
    {
      Transform = Transform.Translated(Vector3.Left * MoveSpeed);
    }

    if (Input.IsActionPressed("ui_right"))
    {
      Transform = Transform.Translated(Vector3.Right * MoveSpeed);
    }

    // Shift
    if (Input.IsActionPressed("ui_crouch"))
    {
      Transform = Transform.Translated(Vector3.Down * MoveSpeed);
    }

    // Space
    if (Input.IsActionPressed("ui_up"))
    {
      Transform = Transform.Translated(Vector3.Up * MoveSpeed);
    }
  }
}
