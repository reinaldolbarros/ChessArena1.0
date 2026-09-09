using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Plugin.Maui.Audio;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace ChessMAUI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
;

		builder.AddAudio();

#if DEBUG
		builder.Logging.AddDebug();
#endif

#if WINDOWS
		// Abre o app já maximizado — evita o usuário precisar clicar em
		// "Maximizar" manualmente, ação que dispara um crash nativo no
		// GraphicsView/Win2D ainda não resolvido.
		builder.ConfigureLifecycleEvents(events =>
		{
			events.AddWindows(windows => windows.OnWindowCreated(window =>
			{
				var handle    = WindowNative.GetWindowHandle(window);
				var windowId  = Win32Interop.GetWindowIdFromWindow(handle);
				var appWindow = AppWindow.GetFromWindowId(windowId);
				if (appWindow?.Presenter is OverlappedPresenter presenter)
					presenter.Maximize();
			}));
		});
#endif

		return builder.Build();
	}
}
