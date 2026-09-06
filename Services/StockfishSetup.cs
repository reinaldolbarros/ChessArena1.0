namespace ChessMAUI.Services;

/// <summary>
/// Locates the Stockfish binary. On Android it ships as a native library (libstockfish.so)
/// so the OS extracts it to NativeLibraryDir — a directory with exec permission.
/// </summary>
public static class StockfishSetup
{
#if ANDROID
    public static Task<string?> GetBinaryPathAsync()
    {
        // The binary ships as a native library (.so) and is extracted by Android to
        // NativeLibraryDir — a directory with exec permission, bypassing SELinux W^X.
        var nativeLibDir = Android.App.Application.Context.ApplicationInfo!.NativeLibraryDir!;
        var binaryPath   = Path.Combine(nativeLibDir, "libstockfish.so");
        System.Diagnostics.Debug.WriteLine($"[SF-Setup] Caminho: {binaryPath}  existe={File.Exists(binaryPath)}");
        return Task.FromResult<string?>(File.Exists(binaryPath) ? binaryPath : null);
    }

#elif WINDOWS
    public static Task<string?> GetBinaryPathAsync()
    {
        var sfPath = Path.Combine(AppContext.BaseDirectory, "stockfish.exe");
        return Task.FromResult<string?>(File.Exists(sfPath) ? sfPath : null);
    }

#else
    public static Task<string?> GetBinaryPathAsync()
        => Task.FromResult<string?>(null);
#endif
}
