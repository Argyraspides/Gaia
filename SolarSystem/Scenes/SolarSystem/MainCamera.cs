using Godot;
using System;

public partial class MainCamera : Camera3D
{
    private float m_pitch = 0f;
    private float m_pitchSpeed = 0.001f;
    
    private float m_yaw = 0f;
    private float m_yawSpeed = 0.001f;

    private float m_moveSpeed = 0.1f;
    
    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is InputEventMouseMotion mouseMotion)
        {
            m_yaw -= mouseMotion.Relative.X * m_yawSpeed;
            m_pitch -= mouseMotion.Relative.Y * m_pitchSpeed;
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        ProcessMoveAround();
        ProcessLookAround();
    }

    void ProcessLookAround()
    {
        Transform3D transform = Transform;
        transform.Basis = Basis.Identity;
        Transform = transform;

        RotateObjectLocal(Vector3.Up, m_yaw);
        RotateObjectLocal(Vector3.Right, m_pitch); 
    }

    void ProcessMoveAround()
    {
        
        // WASD
        if (Input.IsActionPressed("ui_forward"))
        {
            Transform = Transform.Translated(Vector3.Forward * m_moveSpeed);
        }
        if (Input.IsActionPressed("ui_backward"))
        {
            Transform = Transform.Translated(Vector3.Back * m_moveSpeed);
        }
        if (Input.IsActionPressed("ui_left"))
        {
            Transform = Transform.Translated(Vector3.Left * m_moveSpeed);
        }
        if (Input.IsActionPressed("ui_right"))
        {
            Transform = Transform.Translated(Vector3.Right * m_moveSpeed);
        }

        // Shift
        if (Input.IsActionPressed("ui_crouch"))
        {
            Transform = Transform.Translated(Vector3.Down * m_moveSpeed);
        }
        
        // Space
        if (Input.IsActionPressed("ui_up"))
        {
            Transform = Transform.Translated(Vector3.Up * m_moveSpeed);
        }

    }
}
