using UnityEngine;
using Photon.Pun;

public class ProyectilBasico : MonoBehaviourPun
{
    public float velocidad = 25f;
    public float dano = 25f;
    public float tiempoDeVida = 3f;

    void Start()
    {
        if (photonView.IsMine)
        {
            Invoke(nameof(DestruirBala), tiempoDeVida);
        }
    }

    void Update()
    {
        transform.Translate(Vector3.forward * velocidad * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Solo el jugador que disparó calcula los impactos
        if (!photonView.IsMine) return;

        // Comprobamos si el objeto impactado tiene un PhotonView
        PhotonView pvImpactado = other.GetComponentInParent<PhotonView>();

        // SI EL OBJETO IMPACTADO PERTENECE AL MISMO JUGADOR QUE DISPARÓ -> IGNORAR
        if (pvImpactado != null && pvImpactado.Owner == photonView.Owner)
        {
            return;
        }

        // Si es otro jugador con vida
        PlayerHealth saludEnemigo = other.GetComponentInParent<PlayerHealth>();
        if (saludEnemigo != null)
        {
            saludEnemigo.photonView.RPC("RecibirDano", RpcTarget.All, dano);
            DestruirBala();
        }
        else
        {
            // Si choca con una pared o el escenario
            DestruirBala();
        }
    }

    private void DestruirBala()
    {
        PhotonNetwork.Destroy(gameObject);
    }
}