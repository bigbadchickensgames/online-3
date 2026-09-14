using UnityEngine;
using Photon.Pun;

public class PickupArma : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    [Header("Datos del Arma")]
    public DatosArma datosArma;

    [Header("Efectos Visuales y Flote")]
    public float velocidadRotacion = 60f;
    public float velocidadFlote = 2.5f;
    public float amplitudFlote = 0.15f;
    public float offsetAltura = 0.8f;

    private Vector3 posicionInicial;

    void Start()
    {
        posicionInicial = transform.position;

        // Si no tiene datos asignados desde el Inspector ni desde InstantiationData, intentamos resolver por nombre de objeto
        if (datosArma == null)
        {
            string nombreLimpio = gameObject.name.Replace("(Clone)", "").Trim();
            CargarDatosArma(nombreLimpio);
        }
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = info.photonView.InstantiationData;
        if (data != null && data.Length > 0 && data[0] is string nombreSO)
        {
            CargarDatosArma(nombreSO);
        }
    }

    private void CargarDatosArma(string nombreSO)
    {
        if (string.IsNullOrEmpty(nombreSO)) return;

        DatosArma cargado = Resources.Load<DatosArma>(nombreSO);

        if (cargado == null)
        {
            DatosArma[] todas = Resources.LoadAll<DatosArma>("");
            cargado = System.Array.Find(todas, a => a.name == nombreSO);
        }

        // Solo sobreescribimos si realmente encontramos el objeto
        if (cargado != null)
        {
            datosArma = cargado;
        }
    }

    void Update()
    {
        // --- SOLUCIÓN MAGICA AQUÍ ---
        // Si el arma ha sido equipada y ahora es hija de un jugador, abortamos la rotación
        if (transform.parent != null) return; 
        // ----------------------------

        transform.Rotate(Vector3.up * velocidadRotacion * Time.deltaTime, Space.World);

        float nuevoY = (posicionInicial.y + offsetAltura) + (Mathf.Sin(Time.time * velocidadFlote) * amplitudFlote);
        transform.position = new Vector3(posicionInicial.x, nuevoY, posicionInicial.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerShooter shooter = other.GetComponentInParent<PlayerShooter>();
        if (shooter == null) return;

        PhotonView pvJugador = shooter.GetComponent<PhotonView>();

        if (pvJugador != null && pvJugador.IsMine)
        {
            if (datosArma == null)
            {
                Debug.LogError($"[PickupArma] No se puede equipar el arma en '{gameObject.name}' porque 'datosArma' es NULL. Asegúrate de mover la Asset de DatosArma dentro de Assets/Resources/.");
                return;
            }

            shooter.EquiparArma(datosArma);

            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(gameObject);
            }
            else
            {
                photonView.RPC(nameof(RPC_SolicitarDestruccion), RpcTarget.MasterClient);
            }
        }
    }

    [PunRPC]
    private void RPC_SolicitarDestruccion()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}