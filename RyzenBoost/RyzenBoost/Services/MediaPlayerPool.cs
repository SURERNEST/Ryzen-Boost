using System;
using System.IO;
using System.Windows.Media;

namespace RyzenBoost.Services
{
    /// <summary>
    /// Reproduce los efectos de sonido empaquetados como recursos (Assets/*.wav).
    /// Usa un MediaPlayer por nombre de sonido para poder solaparlos (p.ej. un
    /// clic mientras suena el de bienvenida) sin que se corten entre sí.
    /// </summary>
    public class MediaPlayerPool
    {
        private readonly System.Collections.Generic.Dictionary<string, MediaPlayer> _players = new();

        public void Play(string soundName)
        {
            if (!_players.TryGetValue(soundName, out var player))
            {
                player = new MediaPlayer();
                var uri = new Uri($"pack://application:,,,/Assets/{soundName}.wav", UriKind.Absolute);
                player.Open(uri);
                _players[soundName] = player;
            }
            player.Stop();
            player.Position = TimeSpan.Zero;
            player.Play();
        }
    }
}
