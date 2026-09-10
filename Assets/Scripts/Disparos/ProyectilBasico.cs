using UnityEngine;
using Photon.Pun;

public class ProyectilBasico : MonoBehaviourPun
{
    public float velocidad = 25f;
    public float dano = 25f;
    public float tiempoDeVida = 3f;

    private bool yaImpacto = false;

    void Start()
    {
        if (photonView.IsMine)
        {
            Invoke(nameof(DestruirBala), tiempoDeVida);
        }
    }

    void Update()
    {
        if (yaImpacto) return;

        transform.Translate(Vector3.forward * velocidad * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (yaImpacto) return;

        PhotonView pvImpactado = other.GetComponentInParent<PhotonView>();

        // Ignorar colisión con la persona que disparó la bala
        if (pvImpactado != null && pvImpactado.Owner == photonView.Owner)
        {
            return;
        }

        // SI YO SOY EL QUE DISPARÓ: Aplico daño y destruyo la bala en la red
        if (photonView.IsMine)
        {
            PlayerHealth saludEnemigo = other.GetComponentInParent<PlayerHealth>();
            if (saludEnemigo != null)
            {
                saludEnemigo.photonView.RPC("RecibirDano", RpcTarget.All, dano);
            }

            yaImpacto = true;
            DestruirBala();
        }
        // SI YO SOY EL QUE RECIBE EL DISPARO: La oculto al instante en mi pantalla para que no atraviese mi cuerpo
        else
        {
            if (pvImpactado != null && pvImpactado.IsMine)
            {
                yaImpacto = true;
                OcultarBalaLocalmente();
            }
        }
    }

    private void OcultarBalaLocalmente()
    {
        // Ocultamos todos los gráficos de la bala de inmediato
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }

    private void DestruirBala()
    {
        PhotonNetwork.Destroy(gameObject);
    }
}