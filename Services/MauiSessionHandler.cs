using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace ChessMAUI.Services;

// IGotrueSessionPersistence é síncrona (sem overloads async), mas SecureStorage do MAUI só
// tem API assíncrona — por isso os .GetAwaiter().GetResult() abaixo. É um bloqueio curto
// (acesso a um keystore local, não rede), aceitável nesse ponto de adaptação específico.
// Trocar de Preferences (guardava o token de sessão em texto claro, legível num aparelho
// comprometido) pra SecureStorage (criptografado pelo SO) é o que importa aqui.
public class MauiSessionHandler : IGotrueSessionPersistence<Session>
{
    private const string Key = "supabase_session";

    public void SaveSession(Session session)
    {
        try { SecureStorage.Default.SetAsync(Key, JsonConvert.SerializeObject(session)).GetAwaiter().GetResult(); }
        catch { }
    }

    public Session? LoadSession()
    {
        try
        {
            var json = SecureStorage.Default.GetAsync(Key).GetAwaiter().GetResult();

            // Migração única: sessão de uma versão anterior que ainda guardava em Preferences
            // (texto claro). Move pro SecureStorage e apaga o rastro antigo.
            if (string.IsNullOrEmpty(json))
            {
                var legacy = Preferences.Default.Get(Key, "");
                if (!string.IsNullOrEmpty(legacy))
                {
                    json = legacy;
                    SecureStorage.Default.SetAsync(Key, legacy).GetAwaiter().GetResult();
                    Preferences.Default.Remove(Key);
                }
            }

            if (string.IsNullOrEmpty(json)) return null;
            return JsonConvert.DeserializeObject<Session>(json);
        }
        catch { return null; }
    }

    public void DestroySession()
    {
        try { SecureStorage.Default.Remove(Key); } catch { }
    }
}
