using UnityEngine;
using Photon.Pun;

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
    public void RecibirDano(float cantidad)
    {
        vidaActual -= cantidad;
        Debug.Log($"El jugador {photonView.Owner.NickName} recibió {cantidad} de daño. Vida restante: {vidaActual}");

        if (vidaActual <= 0 && photonView.IsMine)
        {
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
        }  //si
    }
}