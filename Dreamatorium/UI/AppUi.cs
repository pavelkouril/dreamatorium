using ImGuiNET;

namespace Dreamatorium.UI;

public sealed class AppUi
{
    public void Draw(in FrameInput frameInput)
    {
        ImGui.Begin("Debug");
        ImGui.Text($"Frame: {frameInput.Frame}");
        ImGui.End();
    }
}
