using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

public static class ScoreManager
{
    // Llama a esto cuando alguien mate a otro o haga algo gracioso
    public static void SumarPuntos(Player jugador, int cantidad)
    {
        if (jugador == null) return;

        int puntosActuales = ObtenerPuntos(jugador);
        int nuevosPuntos = puntosActuales + cantidad;

        Hashtable props = new Hashtable { { "Puntos", nuevosPuntos } };
        jugador.SetCustomProperties(props);
    }

    // Método para leer los puntos actuales
    public static int ObtenerPuntos(Player jugador)
    {
        if (jugador.CustomProperties.TryGetValue("Puntos", out object pts))
        {
            return (int)pts;
        }
        return 0; // Si no tiene puntos aún, devuelve 0
    }

    // Llama a esto desde tu Game Manager cuando empiece la Partida 1 de 5
    public static void ReiniciarPuntosPartida()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            Hashtable props = new Hashtable { { "Puntos", 0 } };
            p.SetCustomProperties(props);
        }
    }
}