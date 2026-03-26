using ImGuiNET;

namespace Dreamatorium.UI;

public sealed class AppUi
{
    private bool _hasRequestedFrameCapture;

    public void Draw(in FrameInput frameInput)
    {
        ImGui.Begin("Debug");
        ImGui.Text($"Frame: {frameInput.Frame}");

        if (ImGui.Button("Capture GPU Trace"))
        {
            _hasRequestedFrameCapture = true;
        }

        ImGui.End();
    }

    public bool TryConsumeFrameCaptureRequest()
    {
        bool requested = _hasRequestedFrameCapture;
        _hasRequestedFrameCapture = false;
        return requested;
    }
}
