using UnityEngine;
using Photon.Pun;
using System.Collections;

public class TrampaPlatano : MonoBehaviourPun
{
    [Header("Ajustes del Resbalón")]
    public float fuerzaEmpuje = 15f;
    public float duracionResbalon = 1.2f;

    [Header("Inmunidad Inicial")]
    public float tiempoArmado = 0.5f; 
    private bool estaArmada = false;
    private bool yaFueActivada = false;

    private void Start()
    {
        StartCoroutine(ActivarTrampaConRetraso());
    }

    private IEnumerator ActivarTrampaConRetraso()
    {
        yield return new WaitForSeconds(tiempoArmado);
        estaArmada = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!estaArmada || yaFueActivada) return;

        PlayerController jugador = other.GetComponentInParent<PlayerController>();

        if (jugador != null)
        {
            PhotonView pvJugador = jugador.GetComponent<PhotonView>();

            if (pvJugador != null && pvJugador.IsMine)
            {
                yaFueActivada = true;

                // 1. Delegamos el proceso al PlayerController del jugador impactado
                jugador.AplicarResbalon(fuerzaEmpuje, duracionResbalon);

                // 2. Destruimos la trampa de la red
                if (photonView.IsMine)
                {
                    PhotonNetwork.Destroy(gameObject);
                }
                else
                {
                    photonView.RPC(nameof(DestruirTrampaRPC), photonView.Owner);
                }
            }
        }
    }

    [PunRPC]
    private void DestruirTrampaRPC()
    {
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}