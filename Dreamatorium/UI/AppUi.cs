using ImGuiNET;
using Dreamatorium.Input;

namespace Dreamatorium.UI;

public sealed class AppUi
{
    public const string kNoBufferVisualizationKey = "";
    public const string kNoBufferVisualizationLabel = "None";

    private bool _hasRequestedFrameCapture;
    private readonly List<string> _bufferVisualizationKeys = [kNoBufferVisualizationKey];
    private readonly List<string> _bufferVisualizationLabels = [kNoBufferVisualizationLabel];
    private int _selectedBufferVisualizationIndex;

    public string SelectedBufferVisualizationKey => _bufferVisualizationKeys[_selectedBufferVisualizationIndex];

    public void SetBufferVisualizationOptions(IEnumerable<(string Key, string Label)> bufferVisualizations)
    {
        string selectedKey = SelectedBufferVisualizationKey;
        _bufferVisualizationKeys.Clear();
        _bufferVisualizationLabels.Clear();

        _bufferVisualizationKeys.Add(kNoBufferVisualizationKey);
        _bufferVisualizationLabels.Add(kNoBufferVisualizationLabel);

        foreach (var (key, label) in bufferVisualizations)
        {
            _bufferVisualizationKeys.Add(key);
            _bufferVisualizationLabels.Add(label);
        }

        _selectedBufferVisualizationIndex = _bufferVisualizationKeys.IndexOf(selectedKey);
        if (_selectedBufferVisualizationIndex < 0)
        {
            _selectedBufferVisualizationIndex = 0;
        }
    }

    public void Draw(in FrameInput frameInput)
    {
        ImGui.Begin("Debug");
        ImGui.Text($"Frame: {frameInput.Frame}");

        if (ImGui.BeginCombo("Buffer Visualization", _bufferVisualizationLabels[_selectedBufferVisualizationIndex]))
        {
            for (int i = 0; i < _bufferVisualizationLabels.Count; i++)
            {
                bool isSelected = i == _selectedBufferVisualizationIndex;
                if (ImGui.Selectable(_bufferVisualizationLabels[i], isSelected))
                {
                    _selectedBufferVisualizationIndex = i;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

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
