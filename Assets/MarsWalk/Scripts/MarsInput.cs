using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Teclas da simulação. Funciona tanto com o Input Manager antigo
/// quanto com o novo Input System (padrão no Unity 6).
/// </summary>
public enum MarsKey
{
    Dashboard,   // TAB
    Map,         // M
    Flashlight,  // F
    Sprint,      // Shift esquerdo
    Jump,        // Espaço
    DustStorm,   // G
    TimeSlower,  // 1
    TimeFaster,  // 2
    Respawn,     // R
    Help,        // H
    Escape       // ESC
}

public static class MarsInput
{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    static Key ToKey(MarsKey k)
    {
        switch (k)
        {
            case MarsKey.Dashboard: return Key.Tab;
            case MarsKey.Map: return Key.M;
            case MarsKey.Flashlight: return Key.F;
            case MarsKey.Sprint: return Key.LeftShift;
            case MarsKey.Jump: return Key.Space;
            case MarsKey.DustStorm: return Key.G;
            case MarsKey.TimeSlower: return Key.Digit1;
            case MarsKey.TimeFaster: return Key.Digit2;
            case MarsKey.Respawn: return Key.R;
            case MarsKey.Help: return Key.H;
            default: return Key.Escape;
        }
    }

    public static bool Down(MarsKey k)
    {
        var kb = Keyboard.current;
        return kb != null && kb[ToKey(k)].wasPressedThisFrame;
    }

    public static bool Held(MarsKey k)
    {
        var kb = Keyboard.current;
        return kb != null && kb[ToKey(k)].isPressed;
    }

    public static Vector2 Move()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f)
                - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f);
        float y = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f)
                - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);
        return new Vector2(x, y);
    }

    public static Vector2 Look()
    {
        var m = Mouse.current;
        return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
    }

    public static bool Click()
    {
        var m = Mouse.current;
        return m != null && m.leftButton.wasPressedThisFrame;
    }
#else
    static KeyCode ToKey(MarsKey k)
    {
        switch (k)
        {
            case MarsKey.Dashboard: return KeyCode.Tab;
            case MarsKey.Map: return KeyCode.M;
            case MarsKey.Flashlight: return KeyCode.F;
            case MarsKey.Sprint: return KeyCode.LeftShift;
            case MarsKey.Jump: return KeyCode.Space;
            case MarsKey.DustStorm: return KeyCode.G;
            case MarsKey.TimeSlower: return KeyCode.Alpha1;
            case MarsKey.TimeFaster: return KeyCode.Alpha2;
            case MarsKey.Respawn: return KeyCode.R;
            case MarsKey.Help: return KeyCode.H;
            default: return KeyCode.Escape;
        }
    }

    public static bool Down(MarsKey k) { return Input.GetKeyDown(ToKey(k)); }
    public static bool Held(MarsKey k) { return Input.GetKey(ToKey(k)); }

    public static Vector2 Move()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    public static Vector2 Look()
    {
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    public static bool Click() { return Input.GetMouseButtonDown(0); }
#endif
}
