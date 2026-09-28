using System;
using System.Diagnostics;
using KeySonic.Core.Audio;
using KeySonic.Core.Keyboard;

Console.WriteLine("KeySonic Phase 1 test harness");
Console.WriteLine("==============================");
Console.WriteLine();

var soundsFolder = args.Length > 0 ? args[0] : System.IO.Path.Combine(AppContext.BaseDirectory, "sounds");
Console.WriteLine($"Loading sounds from: {soundsFolder}");

var soundBank = new SoundBank();
try
{
    soundBank.LoadFromFolder(soundsFolder);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL: could not load sound bank: {ex.Message}");
    Console.Error.WriteLine("Put at least one .wav file in the sounds folder (see README) and try again.");
    return;
}

using var audioEngine = new AudioEngine { MasterVolume = 0.7f };
using var hook = new GlobalKeyboardHook();

var stopwatch = Stopwatch.StartNew();

hook.HookError += (_, ex) =>
{
    Console.Error.WriteLine($"[HOOK ERROR] {ex}");
};

hook.KeyDown += (_, e) =>
{
    var receivedAt = stopwatch.Elapsed;

    var sound = soundBank.PickSound(e.Key);
    audioEngine.Play(sound);

    var dispatchedAt = stopwatch.Elapsed;
    var internalLatencyMs = (dispatchedAt - receivedAt).TotalMilliseconds;

    Console.WriteLine(
        $"{e.TimestampUtc:HH:mm:ss.fff}  key={e.Key,-10} repeat={e.IsRepeat,-5} " +
        $"hook->playback dispatch: {internalLatencyMs:F3} ms");
};

hook.Start();

Console.WriteLine();
Console.WriteLine("Hook installed. Switch to any other window (Notepad, browser, etc.) and start typing.");
Console.WriteLine("This console will keep logging key events and dispatch latency.");
Console.WriteLine("Press Ctrl+C here to exit.");
Console.WriteLine();

// Keep the process alive; the hook does its work on its own dedicated thread.
var exitSignal = new System.Threading.ManualResetEventSlim(false);
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    exitSignal.Set();
};
exitSignal.Wait();

Console.WriteLine("Shutting down cleanly...");
