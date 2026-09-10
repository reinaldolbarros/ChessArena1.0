namespace ChessMAUI.Models;

// Estilo de jogo do bot, independente da força (Skill Level/profundidade). "Balanced" é o
// comportamento de sempre — o motor sempre pega o melhor lance do Stockfish, sem nenhum viés.
// Os demais escolhem entre os melhores candidatos (MultiPV) dentro de uma margem de avaliação,
// preferindo capturas — nunca abrem mão de um lance dentro da margem que evite um mate ou perca
// material fora dela.
public enum BotPersonality
{
    Balanced,
    Aggressive,
    Solid,
}
