using Microsoft.Maui.ApplicationModel.Communication;

namespace ChessMAUI.Services;

public static class EmailService
{
    private const string Chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public static string GenerateCode() =>
        Random.Shared.Next(100000, 999999).ToString();

    public static string GenerateTempPassword() =>
        new(Enumerable.Range(0, 8).Select(_ => Chars[Random.Shared.Next(Chars.Length)]).ToArray());

    public static async Task<bool> SendVerificationCode(string toEmail, string code)
    {
        try
        {
            await Email.ComposeAsync(new EmailMessage
            {
                Subject = "Verificação de conta – Xadrez",
                Body    = $"Seu código de verificação é:\n\n  {code}\n\nInsira esse código no app para concluir o cadastro.",
                To      = { toEmail }
            });
            return true;
        }
        catch { return false; }
    }

    public static async Task<bool> SendTempPassword(string toEmail, string tempPassword)
    {
        try
        {
            await Email.ComposeAsync(new EmailMessage
            {
                Subject = "Nova senha – Xadrez",
                Body    = $"Sua nova senha temporária é:\n\n  {tempPassword}\n\nEntre no app e altere para uma senha de sua escolha em Perfil → Alterar Senha.",
                To      = { toEmail }
            });
            return true;
        }
        catch { return false; }
    }
}
