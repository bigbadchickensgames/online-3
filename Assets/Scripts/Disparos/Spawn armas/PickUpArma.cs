using UnityEngine;
using Photon.Pun;

public class PickupArma :
    MonoBehaviourPun,
    IPunInstantiateMagicCallback
{
    [Header("Datos del Arma")]
    public DatosArma datosArma;

    [Header("Efectos Visuales y Flote")]
    public float velocidadRotacion = 60f;
    public float velocidadFlote = 2.5f;
    public float amplitudFlote = 0.15f;

    [Tooltip("Altura a la que flota POR ENCIMA del punto de spawn. Súbelo si el arma queda enterrada en el suelo o dentro de la base.")]
    public float offsetAltura = 0.8f;

    [Tooltip("Opcional: objeto hijo que gira. Si se deja vacío, gira el objeto raíz (comportamiento del resto de armas).")]
    public Transform modeloVisual;

    private Vector3 posicionInicial;
    private bool estaEquipado = false;

    private void Awake()
    {
        // Un arma que además sirve de proyectil puede traer Rigidbody.
        // Si tiene gravedad, se hunde en el suelo y la física pisa el flote.
        Rigidbody cuerpo = GetComponent<Rigidbody>();

        if (cuerpo != null)
        {
            cuerpo.velocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            cuerpo.useGravity = false;
            cuerpo.isKinematic = true;
        }
    }

    private void Start()
    {
        posicionInicial = transform.position;

        ActualizarEstadoEquipado();

        // Colocamos el arma a su altura de flote de inmediato,
        // para que no aparezca un frame metida en el suelo.
        if (!estaEquipado)
        {
            transform.position =
                new Vector3(
                    posicionInicial.x,
                    posicionInicial.y + offsetAltura,
                    posicionInicial.z
                );
        }

        // Si no tiene datos asignados desde Inspector
        // ni desde InstantiationData, resolver por nombre.
        if (datosArma == null)
        {
            string nombreLimpio =
                gameObject.name
                    .Replace("(Clone)", "")
                    .Trim();

            CargarDatosArma(nombreLimpio);
        }
    }

    // Unity llama a esto automáticamente al cambiar de padre.
    // Así no hay que preguntar por el padre en cada frame.
    private void OnTransformParentChanged()
    {
        ActualizarEstadoEquipado();
    }

    private void ActualizarEstadoEquipado()
    {
        // Solo se considera "equipado" si cuelga de un jugador.
        // Estar dentro de un punto de spawn NO cuenta,
        // así que el arma sigue girando y flotando en la base.
        estaEquipado =
            GetComponentInParent<PlayerShooter>() != null;
    }

    public void OnPhotonInstantiate(
        PhotonMessageInfo info
    )
    {
        object[] data =
            info.photonView.InstantiationData;

        if (
            data != null &&
            data.Length > 0 &&
            data[0] is string nombreSO
        )
        {
            CargarDatosArma(nombreSO);
        }
    }

    private void CargarDatosArma(
        string nombreSO
    )
    {
        if (string.IsNullOrEmpty(nombreSO))
            return;

        DatosArma cargado =
            Resources.Load<DatosArma>(
                nombreSO
            );

        if (cargado == null)
        {
            DatosArma[] todas =
                Resources.LoadAll<DatosArma>("");

            cargado =
                System.Array.Find(
                    todas,
                    a => a.name == nombreSO
                );
        }

        if (cargado != null)
        {
            datosArma = cargado;
        }
    }

    private void Update()
    {
        // Si está equipado en la mano de un jugador,
        // no debe rotar ni flotar.
        if (estaEquipado)
            return;

        Transform objetivoDeGiro =
            modeloVisual != null ? modeloVisual : transform;

        objetivoDeGiro.Rotate(
            Vector3.up *
            velocidadRotacion *
            Time.deltaTime,
            Space.World
        );

        float nuevoY =
            (
                posicionInicial.y +
                offsetAltura
            ) +
            (
                Mathf.Sin(
                    Time.time *
                    velocidadFlote
                ) *
                amplitudFlote
            );

        transform.position =
            new Vector3(
                posicionInicial.x,
                nuevoY,
                posicionInicial.z
            );
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        // Si el pickup ya no existe realmente,
        // no hacemos nada.
        if (photonView == null)
            return;

        PlayerShooter shooter =
            other.GetComponentInParent<PlayerShooter>();

        if (shooter == null)
            return;

        PhotonView pvJugador =
            shooter.GetComponent<PhotonView>();

        if (
            pvJugador == null ||
            !pvJugador.IsMine
        )
        {
            return;
        }

        if (datosArma == null)
        {
            Debug.LogError(
                $"[PickupArma] No se puede equipar el arma en '{gameObject.name}' porque 'datosArma' es NULL. Asegúrate de mover la Asset de DatosArma dentro de Assets/Resources/."
            );

            return;
        }

        // Equipamos el arma al jugador.
        shooter.EquiparArma(
            datosArma
        );

        // El objeto Pickup es Photon.
        // Solamente el Master debe destruirlo.
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(
                gameObject
            );
        }
        else
        {
            photonView.RPC(
                nameof(
                    RPC_SolicitarDestruccion
                ),
                RpcTarget.MasterClient
            );
        }
    }

    [PunRPC]
    private void RPC_SolicitarDestruccion()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (gameObject != null)
        {
            PhotonNetwork.Destroy(
                gameObject
            );
        }
    }

    private void OnDestroy()
    {
        // No hacemos nada aquí deliberadamente.
        //
        // GeneradorArmas detecta que su referencia
        // ha quedado en null y se encarga del respawn.
    }
}