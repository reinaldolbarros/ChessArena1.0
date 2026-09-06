using Android.App;
using Android.Runtime;

namespace ChessMAUI;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
		{
			Android.Util.Log.Error("CHESS_CRASH", args.Exception?.ToString() ?? "null");
			args.Handled = false;
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
