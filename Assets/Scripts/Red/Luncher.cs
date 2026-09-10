using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable; // Alias para resolver la ambigüedad

public class Luncher : MonoBehaviourPunCallbacks
{
    public static Luncher Instancia;

    [Header("Configuración de Personaje")]
    public GameObject prefab;
    public Transform[] puntosDeSpawn;

    [Header("UI de Transición / Fin de Ronda")]
    public UIResultadosController uiResultados;

    private GameObject jugadorLocalActual;
    private int jugadoresVivos = 0;

    private void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (uiResultados == null)
        {
            uiResultados = FindFirstObjectByType<UIResultadosController>();
            if (uiResultados == null)
            {
                GameObject go = new GameObject("UIResultadosController");
                uiResultados = go.AddComponent<UIResultadosController>();
            }
        }

        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
        {
            EstablecerEstadoListoLocal(false);
            SpawnearJugador();
        }
    }

    public void SpawnearJugador()
    {
        if (jugadorLocalActual != null)
        {
            PhotonNetwork.Destroy(jugadorLocalActual);
        }

        if (prefab == null)
        {
            Debug.LogError("Error: No has asignado el prefab en el Inspector del script Luncher.");
            return;
        }

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        if (puntosDeSpawn != null && puntosDeSpawn.Length > 0)
        {
            int indice = Random.Range(0, puntosDeSpawn.Length);
            pos = puntosDeSpawn[indice].position;
            rot = puntosDeSpawn[indice].rotation;
        }

        jugadorLocalActual = PhotonNetwork.Instantiate(prefab.name, pos, rot);

        if (uiResultados != null) uiResultados.Ocultar();

        photonView.RPC(nameof(RPC_NotificarSpawn), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RPC_NotificarSpawn()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            jugadoresVivos++;
        }
    }

    public void NotificarMuerteLocal(PlayerController jugadorQueMurio)
    {
        ActivarModoEspectador();
        photonView.RPC(nameof(RPC_RegistrarMuerte), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.NickName);
    }

    private void ActivarModoEspectador()
    {
        PlayerController[] todosLosJugadores = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in todosLosJugadores)
        {
            if (!p.photonView.IsMine && p.EstaVivo)
            {
                p.EnfocarCamaraEspectador();
                break;
            }
        }
    }

    [PunRPC]
    private void RPC_RegistrarMuerte(string nombreMuerto)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        jugadoresVivos--;

        if (jugadoresVivos <= 1)
        {
            string ganador = "¡Empate!";

            PlayerController[] jugadores = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in jugadores)
            {
                if (p.EstaVivo)
                {
                    ganador = p.photonView.Owner.NickName;
                    break;
                }
            }

            photonView.RPC(nameof(RPC_MostrarFinDeRonda), RpcTarget.All, ganador);
        }
    }

    [PunRPC]
    private void RPC_MostrarFinDeRonda(string ganador)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (uiResultados != null)
        {
            uiResultados.MostrarResultados(ganador);
        }
    }

    public void ToggleEstadoListoLocal()
    {
        bool estadoActual = false;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("IsReady", out object isReady))
        {
            estadoActual = (bool)isReady;
        }

        EstablecerEstadoListoLocal(!estadoActual);
    }

    private void EstablecerEstadoListoLocal(bool estado)
    {
        Hashtable props = new Hashtable { { "IsReady", estado } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            if (uiResultados != null)
            {
                uiResultados.ActualizarEstadoJugadores();
            }

            ComprobarTodosListos();
        }
    }

    private void ComprobarTodosListos()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (!p.CustomProperties.TryGetValue("IsReady", out object isReady) || !(bool)isReady)
            {
                return;
            }
        }

        ResetearEstadosListoYReiniciar();
    }

    private void ResetearEstadosListoYReiniciar()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            Hashtable props = new Hashtable { { "IsReady", false } };
            p.SetCustomProperties(props);
        }

        jugadoresVivos = 0;
        photonView.RPC(nameof(RPC_ReiniciarRonda), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_ReiniciarRonda()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        LimpiarEscenario(); // NUEVO: Llamamos a la limpieza general

        SpawnearJugador();
    }

    // NUEVO: Función para destruir todos los objetos residuales de la ronda anterior
    private void LimpiarEscenario()
    {
        // 1. Limpiar todas las trampas (cada jugador destruye las que él mismo lanzó)
        TrampaPlatano[] trampas = FindObjectsByType<TrampaPlatano>(FindObjectsSortMode.None);
        foreach (TrampaPlatano trampa in trampas)
        {
            if (trampa.photonView.IsMine)
            {
                PhotonNetwork.Destroy(trampa.gameObject);
            }
        }

        // 2. Por si acaso hay balas volando en el momento que termina la ronda, las limpiamos también
        ProyectilBasico[] balas = FindObjectsByType<ProyectilBasico>(FindObjectsSortMode.None);
        foreach (ProyectilBasico bala in balas)
        {
            if (bala.photonView.IsMine)
            {
                PhotonNetwork.Destroy(bala.gameObject);
            }
        }
    }
}