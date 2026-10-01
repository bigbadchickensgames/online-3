using UnityEngine;
using Photon.Pun;

public class ProyectilBasico : MonoBehaviourPun
{
    public float velocidad = 25f;
    public float dano = 25f;
    public float tiempoDeVida = 3f;

<<<<<<< Updated upstream
    void Start()
=======
    [HideInInspector]
    public int duenoActorNumber = -1;

    protected Rigidbody rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public virtual void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] instantiationData = info.photonView.InstantiationData;
        if (instantiationData != null && instantiationData.Length >= 6)
        {
            if (instantiationData[1] is int actorNum)
            {
                duenoActorNumber = actorNum;
            }
            if (instantiationData[2] is float vel)
            {
                velocidad = vel;
            }
            if (instantiationData[3] is float danoFloat)
            {
                dano = Mathf.RoundToInt(danoFloat);
            }
            else if (instantiationData[3] is int danoInt)
            {
                dano = danoInt;
            }
            if (instantiationData[4] is string titular)
            {
                titularMuerte = titular;
            }
            if (instantiationData[5] is int puntos)
            {
                puntosPorBaja = puntos;
            }
        }
    }

    protected virtual void Start()
    {
        if (duenoActorNumber == -1 && photonView != null && photonView.Owner != null)
        {
            duenoActorNumber = photonView.Owner.ActorNumber;
        }

        Destroy(gameObject, tiempoVida);

        IniciarMovimiento();
    }

    protected virtual void IniciarMovimiento()
    {
        if (rb != null)
        {
            rb.velocity = transform.forward * velocidad;
        }
    }

    protected virtual void Update()
    {
        if (rb == null)
        {
            transform.Translate(Vector3.forward * velocidad * Time.deltaTime);
        }
    }

    [PunRPC]
    public virtual void RedirigirProyectil(Vector3 nuevaDireccion, int nuevoDuenoActorNumber)
    {
        duenoActorNumber = nuevoDuenoActorNumber;

        if (nuevaDireccion != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(nuevaDireccion);
        }

        if (rb != null)
        {
            rb.velocity = nuevaDireccion.normalized * velocidad;
        }

        Debug.Log($"<color=cyan>[PROYECTIL] Redirigido correctamente por el jugador con ActorNumber: {nuevoDuenoActorNumber}</color>");
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonView pvImpactado = other.GetComponentInParent<PhotonView>();

        if (pvImpactado != null && pvImpactado.Owner != null && pvImpactado.Owner.ActorNumber == duenoActorNumber)
        {
            return;
        }

        PlayerHealth salud = other.GetComponentInParent<PlayerHealth>();
        if (salud != null)
        {
            salud.photonView.RPC("RecibirDano", RpcTarget.All, dano, duenoActorNumber, titularMuerte, puntosPorBaja);
        }

        DestruirProyectil();
    }

    protected virtual void DestruirProyectil()
>>>>>>> Stashed changes
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