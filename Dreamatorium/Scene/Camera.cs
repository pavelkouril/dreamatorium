using System.Numerics;
using Dreamatorium.Input;
using Dreamatorium.Utils;

namespace Dreamatorium.Scene;

public class Camera(float aspectRatio)
{
    private const float kMinPitchDegrees = -89.0f;
    private const float kMaxPitchDegrees = 89.0f;

    private const float kRotationSensitivity = 0.15f;
    private const float kPanSensitivity = 0.018f;
    private const float kWheelDollySpeed = 2.5f;
    private const float kDragMoveLateralSensitivity = 0.025f;
    private const float kDragMoveForwardSensitivity = 0.06f;
    private const float kKeyboardFlySpeed = 10.0f;
    private const float kKeyboardFlyFastMultiplier = 3.0f;

    private const float kMoveSmoothing = 14.0f;

    private float _yawAngle = 90.0f;

    private float _pitchAngle = 0.0f;

    private Vector2 _previousMousePosition;

    private bool _hasPreviousMousePosition;

    private Vector3 _movementVelocity;

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
        var mousePosition = new Vector2(frameInput.Input.MouseX, frameInput.Input.MouseY);

        if (!_hasPreviousMousePosition)
        {
            _previousMousePosition = mousePosition;
            _hasPreviousMousePosition = true;
        }

        Vector2 mouseDelta = mousePosition - _previousMousePosition;
        _previousMousePosition = mousePosition;

        float deltaTime = float.Max(frameInput.DeltaTime, 1.0f / 1000.0f);

        bool rotateHeld = frameInput.IsMouseDown(0);
        bool panHeld = frameInput.IsMouseDown(2);
        bool moveHeld = frameInput.IsMouseDown(1);

        if (rotateHeld)
        {
            _yawAngle += mouseDelta.X * kRotationSensitivity;
            _pitchAngle = float.Clamp(_pitchAngle - mouseDelta.Y * kRotationSensitivity, kMinPitchDegrees, kMaxPitchDegrees);
            Rotation = Quaternion.CreateFromYawPitchRoll(MathExtensions.Deg2Rad(_yawAngle), MathExtensions.Deg2Rad(_pitchAngle), 0);
        }

        if (panHeld)
        {
            Position += (-Right * mouseDelta.X + Up * mouseDelta.Y) * kPanSensitivity;
        }

        if (frameInput.Input.MouseWheelY != 0.0f)
        {
            Position += Forward * (frameInput.Input.MouseWheelY * kWheelDollySpeed);
        }

        Vector3 desiredVelocity = Vector3.Zero;
        if (moveHeld)
        {
            desiredVelocity += Right * (mouseDelta.X * kDragMoveLateralSensitivity);
            desiredVelocity += Forward * (-mouseDelta.Y * kDragMoveForwardSensitivity);
        }

        if (rotateHeld)
        {
            Vector3 flyDirection = Vector3.Zero;
            if (frameInput.IsKeyDown(KeyCode.W))
            {
                flyDirection += Forward;
            }

            if (frameInput.IsKeyDown(KeyCode.S))
            {
                flyDirection -= Forward;
            }

            if (frameInput.IsKeyDown(KeyCode.D))
            {
                flyDirection += Right;
            }

            if (frameInput.IsKeyDown(KeyCode.A))
            {
                flyDirection -= Right;
            }

            if (flyDirection.LengthSquared() > 0.0f)
            {
                flyDirection = Vector3.Normalize(flyDirection);
                float flySpeed = kKeyboardFlySpeed;
                if (frameInput.IsKeyDown(KeyCode.LeftShift) || frameInput.IsKeyDown(KeyCode.RightShift))
                {
                    flySpeed *= kKeyboardFlyFastMultiplier;
                }

                desiredVelocity += flyDirection * flySpeed;
            }
        }

        float blend = 1.0f - MathF.Exp(-kMoveSmoothing * deltaTime);
        _movementVelocity = Vector3.Lerp(_movementVelocity, desiredVelocity, blend);
        Position += _movementVelocity * deltaTime;
    }
}
