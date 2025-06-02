using System.Numerics;
using Dreamatorium.Input;
using Dreamatorium.Utils;

namespace Dreamatorium.Scene;

public class Camera(float aspectRatio)
{
    private float _yawAngle = 90;

    private float _pitchAngle = 0;

    public Vector3 Position { get; private set; } = new(0, 5, 0);

    public Quaternion Rotation { get; private set; } = Quaternion.CreateFromYawPitchRoll(MathExtensions.Deg2Rad(90), 0, 0);

    public float FieldOfViewInDegrees { get; } = 60;

    public float NearPlaneDistance { get; } = 0.1f;

    public float FarPlaneDistance { get; } = 1000.0f;

    public float AspectRatio { get; set; } = aspectRatio;

    public Vector3 Right => Rotation.RotateVector(Vector3.UnitX);

    public Vector3 Up => Rotation.RotateVector(Vector3.UnitY);

    public Vector3 Forward => Rotation.RotateVector(Vector3.UnitZ);

    public Matrix4x4 WorldToCameraMatrix => Matrix4x4.CreateLookAtLeftHanded(Position, Position + Forward, Up);

    public Matrix4x4 ProjectionMatrix => Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(MathExtensions.Deg2Rad(FieldOfViewInDegrees), AspectRatio, NearPlaneDistance, FarPlaneDistance);

    public void ProcessInput(in FrameInput frameInput)
    {
        if (frameInput.HasKeyEvent(KeyCode.W, KeyEventType.KeyDown))
        {
            Position += Forward;
        }

        if (frameInput.HasKeyEvent(KeyCode.S, KeyEventType.KeyDown))
        {
            Position -= Forward;
        }

        if (frameInput.HasKeyEvent(KeyCode.A, KeyEventType.KeyDown))
        {
            Position -= Right;
        }

        if (frameInput.HasKeyEvent(KeyCode.D, KeyEventType.KeyDown))
        {
            Position += Right;
        }

        if (frameInput.HasKeyEvent(KeyCode.Q, KeyEventType.KeyDown))
        {
            Position -= Up;
        }

        if (frameInput.HasKeyEvent(KeyCode.E, KeyEventType.KeyDown))
        {
            Position += Up;
        }

        if (frameInput.HasKeyEvent(KeyCode.Z, KeyEventType.KeyDown))
        {
            _yawAngle -= 10;
            Rotation = Quaternion.CreateFromYawPitchRoll(MathExtensions.Deg2Rad(_yawAngle), MathExtensions.Deg2Rad(_pitchAngle), 0);
        }

        if (frameInput.HasKeyEvent(KeyCode.X, KeyEventType.KeyDown))
        {
            _yawAngle += 10;
            Rotation = Quaternion.CreateFromYawPitchRoll(MathExtensions.Deg2Rad(_yawAngle), MathExtensions.Deg2Rad(_pitchAngle), 0);
        }
    }
}
