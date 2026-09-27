using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using static KeySonic.Core.Keyboard.NativeMethods;

namespace KeySonic.Core.Keyboard;

/// <summary>
/// Installs a WH_KEYBOARD_LL hook and raises a normalized KeyDown event for every
/// physical key press, anywhere on the system, regardless of which window has focus.
///
/// Design notes (read before touching this file):
///   - The hook callback MUST stay extremely fast. Windows enforces a timeout on
///     low-level hooks; if the callback blocks too long, Windows silently removes
///     the hook and KeySonic goes deaf until restarted. Do not do file I/O, locking,
///     or heavy work in HookCallback. Just classify the key and raise the event.
///   - WH_KEYBOARD_LL requires the installing thread to pump Windows messages, so
///     this class spins up its own dedicated thread with a manual GetMessage loop.
///     A console app has no message pump of its own, and we don't want to force
///     WinForms/WPF onto the core engine, so we bring our own tiny one.
///   - We NEVER swallow or replace the key event (we don't return a non-zero value
///     from the hook proc), and we always call CallNextHookEx. This app only
///     observes; it never remaps or blocks input.
///   - Auto-repeat: Windows re-fires KEYDOWN for a held key. We track which VKs
///     are currently down so callers can decide whether repeats should re-trigger
///     sound (real mechanical keyboards do repeat-click while held, so the default
///     behavior here is to still raise the event with IsRepeat = true).
///   - Injected input (SendInput / other software) is ignored by default to avoid
///     feedback loops with other utilities.
/// </summary>
public sealed class GlobalKeyboardHook : IDisposable
{
    public event EventHandler<KeyEventData>? KeyDown;
    public event EventHandler<Exception>? HookError;

    private readonly LowLevelKeyboardProc _proc;
    private readonly HashSet<uint> _currentlyDown = new();
    private readonly object _stateLock = new();

    private IntPtr _hookHandle = IntPtr.Zero;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private volatile bool _running;

    public GlobalKeyboardHook()
    {
        // Keep a reference to the delegate for the lifetime of this object so the
        // GC never collects it while native code still holds a function pointer to it.
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        _hookThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "KeySonic.KeyboardHookThread"
        };
        _hookThread.Start();
    }

    private void RunMessageLoop()
    {
        _hookThreadId = GetCurrentThreadId();

        using var curModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        var hMod = GetModuleHandle(curModule?.ModuleName ?? string.Empty);

        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hMod, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            HookError?.Invoke(this, new InvalidOperationException($"SetWindowsHookEx failed, Win32 error {err}"));
            _running = false;
            return;
        }

        // Manual message pump. Required for WH_KEYBOARD_LL to fire reliably.
        while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Always defensive: an unhandled exception here can crash the whole process
        // because we're on the far side of a native call boundary.
        try
        {
            if (nCode >= 0)
            {
                var msg = (int)wParam;
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (isDown || isUp)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    bool injected = (data.flags & LLKHF_INJECTED) != 0;
                    if (!injected)
                    {
                        if (isDown)
                        {
                            bool isRepeat;
                            lock (_stateLock)
                            {
                                isRepeat = !_currentlyDown.Add(data.vkCode);
                            }

                            var key = VirtualKeyMapper.ToKeyCode((int)data.vkCode);
                            if (key != KeyCode.Unknown)
                            {
                                KeyDown?.Invoke(this, new KeyEventData(key, isRepeat, DateTime.UtcNow));
                            }
                        }
                        else
                        {
                            lock (_stateLock)
                            {
                                _currentlyDown.Remove(data.vkCode);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            HookError?.Invoke(this, ex);
        }

        // Always pass the event on unchanged. KeySonic observes; it never blocks or remaps.
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_running) return;
        _running = false;

        if (_hookThreadId != 0)
        {
            PostThreadMessage(_hookThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        }

        _hookThread?.Join(TimeSpan.FromSeconds(2));
        lock (_stateLock)
        {
            _currentlyDown.Clear();
        }
    }
}
