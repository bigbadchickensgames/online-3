using UnityEngine;
using Photon.Pun;

public class ProyectilPuno : ProyectilBasico
{
    [Header("Configuración del Puño")]
    public float fuerzaKnockback = 20f;

    protected override void OnTriggerEnter(Collider other)
    {
        // AUTORIDAD DEL HOST: Si no soy el Master Client, ignoro la colisión completamente.
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView pvImpactado = other.GetComponentInParent<PhotonView>();

        // Evitar fuego amigo consigo mismo
        if (pvImpactado != null && pvImpactado.Owner != null && pvImpactado.Owner.ActorNumber == duenoActorNumber)
        {
            return;
        }

        PlayerHealth salud = other.GetComponentInParent<PlayerHealth>();
        if (salud != null)
        {
            // 1. El Host ordena aplicar daño a todos
            salud.photonView.RPC("RecibirDano", RpcTarget.All, dano, duenoActorNumber, titularMuerte, puntosPorBaja);

            PlayerController controller = other.GetComponentInParent<PlayerController>();
            if (controller != null)
            {
                // 2. El Host calcula la matemática del empuje
                Vector3 direccionKnockback = (controller.transform.position - transform.position).normalized;

                // 3. El Host dispara la orden de movimiento
                controller.AplicarKnockbackRed(direccionKnockback, fuerzaKnockback);
            }
        }

        // El Host destruye el proyectil en la red
        DestruirProyectil();
    }
}
