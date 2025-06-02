namespace Dreamatorium.Input;

[Flags]
public enum KeyEventType
{
    KeyUp = 1,
    KeyDown = 1 << 1,
    IsRepeat = 1 << 2,
}

public class InputManager
{
    private readonly byte[][] _buffers = [new byte[256], new byte[256]];

    private int _currentIndex = 0;

    private readonly Lock _lock = new();

    public void RecordKeyEvent(ushort keyCode, KeyEventType type, bool isRepeat)
    {
        lock (_lock)
        {
            byte[] keys = _buffers[_currentIndex];
            keys[keyCode] |= (byte)type;
            if (isRepeat)
            {
                keys[keyCode] |= (byte)KeyEventType.IsRepeat;
            }
        }
    }

    public byte[] ReturnCurrentBufferAndSwap()
    {
        lock (_lock)
        {
            byte[] current = _buffers[_currentIndex];
            // swap to a new one and clear
            _currentIndex = (_currentIndex + 1) % _buffers.Length;
            Array.Clear(_buffers[_currentIndex]);
            return current;
        }
    }
}
