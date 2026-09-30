using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using KeySonic.Core.Keyboard;
using static KeySonic.Core.Keyboard.NativeMethods;

namespace KeySonic.Core.Mouse;

public sealed class GlobalMouseHook : IDisposable
{
    public event EventHandler<MouseButtonEventArgs>? ButtonDown;
    public event EventHandler<Exception>? HookError;

    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookHandle;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private volatile bool _running;

    public GlobalMouseHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _hookThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "KeySonic.MouseHookThread"
        };
        _hookThread.Start();
    }

    private void RunMessageLoop()
    {
        _hookThreadId = GetCurrentThreadId();
        using var process = Process.GetCurrentProcess();
        var moduleName = process.MainModule?.ModuleName ?? string.Empty;
        var moduleHandle = GetModuleHandle(moduleName);

        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            HookError?.Invoke(this, new InvalidOperationException($"SetWindowsHookEx failed, Win32 error {error}"));
            _running = false;
            return;
        }

        while (_running && GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                int message = (int)wParam;
                if (message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN)
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if ((data.flags & (LLMHF_INJECTED | LLMHF_LOWER_IL_INJECTED)) == 0)
                    {
                        var button = message == WM_LBUTTONDOWN ? MouseButton.Left : MouseButton.Right;
                        ButtonDown?.Invoke(this, new MouseButtonEventArgs(button));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            HookError?.Invoke(this, ex);
        }

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
    }
}