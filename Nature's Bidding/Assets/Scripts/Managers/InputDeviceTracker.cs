using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class InputDeviceTracker
{
    public enum InputType
    {
        MouseAndKeyboard,
        Gamepad
    }

    public static InputType CurrentInputType { get; private set; } = InputType.MouseAndKeyboard;

    public static event Action<InputType> OnInputTypeChanged;

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        InputSystem.onEvent += OnAnyInputEvent;
    }

    public static void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;

        InputSystem.onEvent -= OnAnyInputEvent;
    }

    private static void OnAnyInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

        InputType newType = device switch
        {
            Gamepad => InputType.Gamepad,
            Mouse => InputType.MouseAndKeyboard,
            Keyboard => InputType.MouseAndKeyboard,
            _ => CurrentInputType
        };

        if (newType != CurrentInputType)
        {
            CurrentInputType = newType;
            Debug.Log($"[InputDeviceTracker] Switched to {CurrentInputType} (triggered by {device.displayName}).");
            OnInputTypeChanged?.Invoke(CurrentInputType);
        }
    }
}