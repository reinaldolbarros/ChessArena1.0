using Foundation;
using UIKit;

namespace ChessMAUI;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Retorno do login Google (WebAuthenticator) e do link de "esqueci senha" — ambos chegam
	// aqui pelo mesmo esquema chessarena://, distinguidos pelo host da URL.
	public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
	{
		if (Microsoft.Maui.ApplicationModel.Platform.OpenUrl(app, url, options))
			return true;

		try { ChessMAUI.Services.DeepLinkRouter.Handle(new Uri(url.AbsoluteString!)); }
		catch { }

		return base.OpenUrl(app, url, options);
	}
}
