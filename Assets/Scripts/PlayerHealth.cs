using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerHealth : MonoBehaviourPun
{
    [Header("Estadísticas")]
    public float vidaMaxima = 100f;

    private float vidaActual;
    private bool haMuerto = false;

    private void Start()
    {
        vidaActual = vidaMaxima;
    }

    // NUEVO: muerte por caer al vacío. Sin puntos.
    // Comparte el flag haMuerto con RecibirDano, así que
    // nunca se registra la muerte dos veces.
    public void MatarPorVacio()
    {
        if (!photonView.IsMine)
            return;

        if (haMuerto)
            return;

        haMuerto = true;

        Debug.Log(
            $"<color=orange>{photonView.Owner.NickName} cayó al vacío.</color>"
        );

        Morir();
    }

    [PunRPC]
    public void RecibirDano(
        int cantidad,
        int actorNumberAtacante,
        string nombreArma,
        int puntosPremio
    )
    {
        // Si ya ha muerto, ignoramos cualquier daño posterior.
        if (haMuerto)
            return;

        vidaActual -= cantidad;
        GetComponent<GameJuicePlayer>()?.HacerFlash();

        Debug.Log(
            $"El jugador {photonView.Owner.NickName} recibió {cantidad} de daño. Vida restante: {vidaActual}"
        );

        if (vidaActual <= 0f && photonView.IsMine)
        {
            haMuerto = true;

            // Buscamos al atacante mediante ActorNumber
            Player atacante = null;

            if (PhotonNetwork.CurrentRoom != null)
            {
                atacante =
                    PhotonNetwork.CurrentRoom.GetPlayer(
                        actorNumberAtacante
                    );
            }

            if (atacante != null)
            {
                if (atacante != photonView.Owner)
                {
                    // NOS MATÓ OTRO JUGADOR
                    ScoreManager.SumarPuntos(
                        atacante,
                        puntosPremio
                    );

                    Debug.Log(
                        $"<color=green>¡{atacante.NickName} humilló a {photonView.Owner.NickName} con {nombreArma}! (+{puntosPremio} pts)</color>"
                    );
                }
                else
                {
                    // AUTO-HUMILLACIÓN
                    ScoreManager.SumarPuntos(
                        photonView.Owner,
                        25
                    );

                    Debug.Log(
                        $"<color=red>¡AUTO-HUMILLACIÓN! {photonView.Owner.NickName} se eliminó a sí mismo. (+25 pts)</color>"
                    );
                }
            }

            Morir();
        }
    }

    private void Morir()
    {
        if (!photonView.IsMine)
            return;

        // =====================================================
        // 1. ELIMINAR EL ARMA VISUAL EN TODOS LOS CLIENTES
        // =====================================================

        photonView.RPC(
            nameof(RPC_LimpiarArmaAlMorir),
            RpcTarget.All
        );

        // =====================================================
        // 2. AVISAR AL LUNCHER
        // =====================================================

        if (Luncher.Instancia != null)
        {
            PlayerController controller =
                GetComponent<PlayerController>();

            Luncher.Instancia.NotificarMuerteLocal(
                controller
            );
        }

        // =====================================================
        // 3. DESTRUIR AL JUGADOR
        // =====================================================

        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    private void RPC_LimpiarArmaAlMorir()
    {
        // Buscamos el PlayerShooter que pertenece
        // a este mismo PlayerHealth.
        PlayerShooter shooter =
            GetComponent<PlayerShooter>();

        if (shooter != null)
        {
            // Elimina explícitamente:
            // - modelo del arma
            // - punto de disparo
            // - munición
            // - datos del arma
            // - animación melee
            shooter.LimpiarArmaVisualLocal();
        }

        // Seguridad adicional:
        // aunque el arma ya no exista, dejamos claro
        // que no debe quedar ninguna referencia.
        transform.GetComponentsInChildren<Transform>(
            true
        );
    }
}