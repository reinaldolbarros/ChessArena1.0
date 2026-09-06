using Plugin.Maui.Audio;
using Enc = System.Text.Encoding;

namespace ChessMAUI.Services;

/// <summary>
/// Gera tons WAV sintetizados em memória e os reproduz via Plugin.Maui.Audio.
/// Não exige arquivos de áudio externos.
/// </summary>
public class SoundService
{
    public bool Enabled { get; set; } = true;

    // Eventos sonoros
    public void PlayMove()     => Fire(440, 0.07);          // Lá — movimento simples
    public void PlayCapture()  => Fire(523, 0.10);          // Dó — captura
    public void PlayCheck()    => Fire(880, 0.13);          // Lá agudo — xeque
    public void PlayGameOver() => Fire(220, 0.55);          // Lá grave — fim de jogo

    private void Fire(double hz, double sec) => _ = PlayAsync(hz, sec);

    private async Task PlayAsync(double hz, double sec)
    {
        if (!Enabled) return;
        try
        {
            var wav = GenerateWav(hz, sec);
            using var ms     = new MemoryStream(wav);
            using var player = AudioManager.Current.CreatePlayer(ms);
            player.Play();
            await Task.Delay((int)(sec * 1000) + 80);
            player.Stop();
        }
        catch { /* áudio não disponível — falha silenciosa */ }
    }

    // -------------------------------------------------------------------------
    // Gerador de WAV PCM 16-bit mono com fade-in/fade-out suave
    // -------------------------------------------------------------------------
    private static byte[] GenerateWav(double frequency, double durationSec)
    {
        const int    sampleRate = 22_050;
        const double amplitude  = 0.35;

        int samples  = (int)(sampleRate * durationSec);
        int dataSize = samples * sizeof(short);
        double fadeIn  = sampleRate * 0.010; // 10 ms fade-in
        double fadeOut = sampleRate * 0.020; // 20 ms fade-out

        using var ms = new MemoryStream(44 + dataSize);
        using var w  = new BinaryWriter(ms, Enc.ASCII, leaveOpen: true);

        // --- Cabeçalho RIFF/WAV ---
        w.Write(Enc.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);               // tamanho do chunk
        w.Write(Enc.ASCII.GetBytes("WAVE"));
        w.Write(Enc.ASCII.GetBytes("fmt "));
        w.Write(16);                          // tamanho do fmt chunk
        w.Write((short)1);                   // PCM
        w.Write((short)1);                   // mono
        w.Write(sampleRate);                 // sample rate
        w.Write(sampleRate * 2);             // byte rate
        w.Write((short)2);                   // block align
        w.Write((short)16);                  // bits per sample
        w.Write(Enc.ASCII.GetBytes("data"));
        w.Write(dataSize);

        // --- Amostras PCM ---
        for (int i = 0; i < samples; i++)
        {
            double t    = (double)i / sampleRate;
            double fade = i < fadeIn               ? i / fadeIn
                        : i > samples - fadeOut    ? (samples - i) / fadeOut
                        : 1.0;
            short s = (short)(short.MaxValue * amplitude
                              * Math.Sin(2 * Math.PI * frequency * t)
                              * fade);
            w.Write(s);
        }

        w.Flush();
        return ms.ToArray();
    }
}
