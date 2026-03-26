namespace Dreamatorium.Input;

public readonly struct InputCaptureState(bool consumeMouse, bool consumeKeyboard)
{
    public readonly bool ConsumeMouse = consumeMouse;

    public readonly bool ConsumeKeyboard = consumeKeyboard;
}
