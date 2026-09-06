using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace ChessMAUI.Services;

public class MauiSessionHandler : IGotrueSessionPersistence<Session>
{
    private const string Key = "supabase_session";

    public void SaveSession(Session session)
    {
        try { Preferences.Default.Set(Key, JsonConvert.SerializeObject(session)); }
        catch { }
    }

    public Session? LoadSession()
    {
        var json = Preferences.Default.Get(Key, "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonConvert.DeserializeObject<Session>(json); }
        catch { return null; }
    }

    public void DestroySession()
        => Preferences.Default.Remove(Key);
}
