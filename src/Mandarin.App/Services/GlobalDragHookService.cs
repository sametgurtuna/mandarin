using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Mandarin.App.Services;

/// <summary>
/// System-wide low-level mouse and keyboard hook that detects when a file or item
/// is being dragged anywhere in Windows while holding Shift (or Shift+Alt).
/// Triggers the Tangerine-style Radial Wheel Menu directly at the cursor.
/// </summary>
public sealed class GlobalDragHookService : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;

    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private const int VK_LBUTTON = 0x01;
    private const int VK_ESCAPE = 0x1B;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    private IntPtr _mouseHookId = IntPtr.Zero;
    private IntPtr _keyboardHookId = IntPtr.Zero;

    private readonly LowLevelHookProc _mouseProc;
    private readonly LowLevelHookProc _keyboardProc;

    private bool _isMouseDown;
    private POINT _mouseDownPos;
    private bool _dragTriggered;
    private bool _disposed;

    public event Action<POINT, bool>? DragShiftDetected;
    public event Action<bool, bool>? ModifiersChanged;
    public event Action? DragCancelled;
    public event Action? DragEnded;

    public GlobalDragHookService()
    {
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public void ResetDragTriggered()
    {
        _dragTriggered = false;
    }

    public void Start()
    {
        if (_mouseHookId != IntPtr.Zero) return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var modHandle = GetModuleHandle(curModule?.ModuleName);

        _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, modHandle, 0);
        _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, modHandle, 0);
    }

    public void Stop()
    {
        if (_mouseHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }

        if (_keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }

        _isMouseDown = false;
        _dragTriggered = false;
    }

    public static bool IsShiftDown()
    {
        return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
    }

    public static bool IsAltDown()
    {
        return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_LBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                _isMouseDown = true;
                _mouseDownPos = hookStruct.pt;
                _dragTriggered = false;
            }
            else if (msg == WM_LBUTTONUP)
            {
                _isMouseDown = false;
                if (_dragTriggered)
                {
                    _dragTriggered = false;
                    DragEnded?.Invoke();
                }
            }
            else if (msg == WM_MOUSEMOVE && _isMouseDown)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int dx = hookStruct.pt.X - _mouseDownPos.X;
                int dy = hookStruct.pt.Y - _mouseDownPos.Y;

                if ((dx * dx + dy * dy) > 25) // Moved > 5px while holding mouse button
                {
                    if (IsShiftDown() && !_dragTriggered)
                    {
                        _dragTriggered = true;
                        DragShiftDetected?.Invoke(hookStruct.pt, IsAltDown());
                    }
                }
            }
        }

        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (kbStruct.vkCode == VK_SHIFT || kbStruct.vkCode == VK_LSHIFT || kbStruct.vkCode == VK_RSHIFT)
            {
                if (isKeyDown)
                {
                    if (_isMouseDown && !_dragTriggered)
                    {
                        _dragTriggered = true;
                        GetCursorPos(out var pt);
                        DragShiftDetected?.Invoke(pt, IsAltDown());
                    }
                }
                else if (isKeyUp)
                {
                    if (_dragTriggered)
                    {
                        _dragTriggered = false;
                        DragCancelled?.Invoke();
                    }
                }
            }
            else if (kbStruct.vkCode == VK_MENU || kbStruct.vkCode == VK_LMENU || kbStruct.vkCode == VK_RMENU)
            {
                if (_dragTriggered)
                {
                    ModifiersChanged?.Invoke(IsShiftDown(), isKeyDown);
                }
            }
            else if (kbStruct.vkCode == VK_ESCAPE && isKeyDown)
            {
                if (_dragTriggered)
                {
                    _dragTriggered = false;
                    DragCancelled?.Invoke();
                }
            }
        }

        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
