using UnityEngine;
using Photon.Pun;
using Photon.Realtime; // Necesario para identificar a los jugadores por su ID

public class PlayerHealth : MonoBehaviourPun
{
    [Header("Estadísticas")]
    public float vidaMaxima = 100f;
    private float vidaActual;

    void Start()
    {
        vidaActual = vidaMaxima;
    }

    // Este atributo permite que la función se ejecute en todos los clientes conectados
    [PunRPC]
    public void RecibirDano(float cantidad, int actorNumberAtacante, string nombreArma, int puntosPremio)
    {
        vidaActual -= cantidad;
        Debug.Log($"El jugador {photonView.Owner.NickName} recibió {cantidad} de daño. Vida restante: {vidaActual}");

        if (vidaActual <= 0 && photonView.IsMine)
        {
            // Buscamos quién nos ha matado en la sala de Photon usando su ActorNumber
            Player atacante = PhotonNetwork.CurrentRoom.GetPlayer(actorNumberAtacante);

            if (atacante != null)
            {
                if (atacante != photonView.Owner)
                {
                    // NOS MATÓ OTRO JUGADOR: Le damos sus puntos
                    ScoreManager.SumarPuntos(atacante, puntosPremio);
                    Debug.Log($"<color=green>¡{atacante.NickName} humilló a {photonView.Owner.NickName} con {nombreArma}! (+{puntosPremio} pts)</color>");
                }
                else
                {
                    // NOS MATAMOS NOSOTROS MISMOS (Auto-humillación)
                    ScoreManager.SumarPuntos(photonView.Owner, 25);
                    Debug.Log($"<color=red>¡AUTO-HUMILLACIÓN! {photonView.Owner.NickName} se eliminó a sí mismo. (+25 pts)</color>");
                }
            }

            Morir();
        }
    }

    private void Morir()
    {
        if (photonView.IsMine)
        {
            // Avisamos al Luncher que hemos muerto pasándole nuestro PlayerController
            if (Luncher.Instancia != null)
            {
                PlayerController controller = GetComponent<PlayerController>();
                Luncher.Instancia.NotificarMuerteLocal(controller);
            }

            PhotonNetwork.Destroy(gameObject);
        }
    }
}