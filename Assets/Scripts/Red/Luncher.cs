using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Luncher : MonoBehaviourPunCallbacks
{
    public static Luncher Instancia;

    [Header("Configuración de Personaje")]
    public GameObject prefab;
    public Transform[] puntosDeSpawn;

    [Header("UI de Transición / Fin de Ronda")]
    public UIResultadosController uiResultados;

    [Header("Configuración de Partida")]
    public int rondaActual = 1;
    public int maxRondas = 5;

    private GameObject jugadorLocalActual;
    private int jugadoresVivos = 0;

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (uiResultados == null)
        {
            uiResultados =
                FindFirstObjectByType<UIResultadosController>();

            if (uiResultados == null)
            {
                GameObject go =
                    new GameObject(
                        "UIResultadosController"
                    );

                uiResultados =
                    go.AddComponent<UIResultadosController>();
            }
        }

        if (
            PhotonNetwork.IsConnectedAndReady &&
            PhotonNetwork.InRoom
        )
        {
            EstablecerEstadoListoLocal(false);
            SpawnearJugador();
        }
    }

    // =========================================================
    // SPAWN
    // =========================================================

    public void SpawnearJugador()
    {
        // Antes de destruir el jugador viejo,
        // eliminamos explícitamente su arma visual.
        if (jugadorLocalActual != null)
        {
            PlayerShooter shooter =
                jugadorLocalActual.GetComponent<PlayerShooter>();

            if (shooter != null)
            {
                shooter.LimpiarArmaVisualLocal();
            }

            PhotonNetwork.Destroy(jugadorLocalActual);
            jugadorLocalActual = null;
        }

        if (prefab == null)
        {
            Debug.LogError(
                "Error: No has asignado el prefab en el Inspector del script Luncher."
            );

            return;
        }

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        if (
            puntosDeSpawn != null &&
            puntosDeSpawn.Length > 0
        )
        {
            int indice =
                Random.Range(
                    0,
                    puntosDeSpawn.Length
                );

            pos =
                puntosDeSpawn[indice].position;

            rot =
                puntosDeSpawn[indice].rotation;
        }

        jugadorLocalActual =
            PhotonNetwork.Instantiate(
                prefab.name,
                pos,
                rot
            );

        if (uiResultados != null)
        {
            uiResultados.Ocultar();
        }

        photonView.RPC(
            nameof(RPC_NotificarSpawn),
            RpcTarget.MasterClient
        );
    }

    [PunRPC]
    private void RPC_NotificarSpawn()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            jugadoresVivos++;
        }
    }

    // =========================================================
    // MUERTE
    // =========================================================

    public void NotificarMuerteLocal(
        PlayerController jugadorQueMurio
    )
    {
        ActivarModoEspectador();

        photonView.RPC(
            nameof(RPC_RegistrarMuerte),
            RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.NickName
        );
    }

    private void ActivarModoEspectador()
    {
        PlayerController[] todosLosJugadores =
            FindObjectsByType<PlayerController>(
                FindObjectsSortMode.None
            );

        foreach (var p in todosLosJugadores)
        {
            if (
                !p.photonView.IsMine &&
                p.EstaVivo
            )
            {
                p.EnfocarCamaraEspectador();
                break;
            }
        }
    }

    [PunRPC]
    private void RPC_RegistrarMuerte(
        string nombreMuerto
    )
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        jugadoresVivos--;

        if (jugadoresVivos <= 1)
        {
            string ganador = "¡Empate!";

            PlayerController[] jugadores =
                FindObjectsByType<PlayerController>(
                    FindObjectsSortMode.None
                );

            foreach (var p in jugadores)
            {
                if (p.EstaVivo)
                {
                    ganador =
                        p.photonView.Owner.NickName;

                    ScoreManager.SumarPuntos(
                        p.photonView.Owner,
                        150
                    );

                    break;
                }
            }

            photonView.RPC(
                nameof(RPC_MostrarFinDeRonda),
                RpcTarget.All,
                ganador,
                rondaActual,
                maxRondas
            );
        }
    }

    // =========================================================
    // FIN DE RONDA
    // =========================================================

    [PunRPC]
    private void RPC_MostrarFinDeRonda(
        string ganador,
        int ronda,
        int maximasRondas
    )
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        if (uiResultados != null)
        {
            uiResultados.MostrarResultados(
                ganador,
                ronda,
                maximasRondas
            );
        }
    }

    // =========================================================
    // READY
    // =========================================================

    public void ToggleEstadoListoLocal()
    {
        bool estadoActual = false;

        if (
            PhotonNetwork.LocalPlayer.CustomProperties
                .TryGetValue(
                    "IsReady",
                    out object isReady
                )
        )
        {
            estadoActual = (bool)isReady;
        }

        EstablecerEstadoListoLocal(
            !estadoActual
        );
    }

    private void EstablecerEstadoListoLocal(
        bool estado
    )
    {
        Hashtable props =
            new Hashtable
            {
                {
                    "IsReady",
                    estado
                }
            };

        PhotonNetwork.LocalPlayer
            .SetCustomProperties(props);
    }

    public override void OnPlayerPropertiesUpdate(
        Player targetPlayer,
        Hashtable changedProps
    )
    {
        if (
            changedProps.ContainsKey(
                "IsReady"
            )
        )
        {
            if (uiResultados != null)
            {
                uiResultados
                    .ActualizarEstadoJugadores();
            }

            ComprobarTodosListos();
        }
    }

    private void ComprobarTodosListos()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        foreach (
            Player p
            in PhotonNetwork.PlayerList
        )
        {
            if (
                !p.CustomProperties.TryGetValue(
                    "IsReady",
                    out object isReady
                ) ||
                !(bool)isReady
            )
            {
                return;
            }
        }

        ResetearEstadosListoYReiniciar();
    }

    private void ResetearEstadosListoYReiniciar()
    {
        foreach (
            Player p
            in PhotonNetwork.PlayerList
        )
        {
            Hashtable props =
                new Hashtable
                {
                    {
                        "IsReady",
                        false
                    }
                };

            p.SetCustomProperties(props);
        }

        jugadoresVivos = 0;

        photonView.RPC(
            nameof(RPC_ReiniciarRonda),
            RpcTarget.All
        );
    }

    // =========================================================
    // REINICIAR RONDA
    // =========================================================

    [PunRPC]
    private void RPC_ReiniciarRonda()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;

        // -----------------------------------------------------
        // AVANCE DE RONDA
        // -----------------------------------------------------

        if (rondaActual >= maxRondas)
        {
            rondaActual = 1;

            ScoreManager.ReiniciarPuntosPartida();
        }
        else
        {
            rondaActual++;
        }

        // -----------------------------------------------------
        // LIMPIEZA COMPLETA
        // -----------------------------------------------------

        LimpiarEscenario();

        // -----------------------------------------------------
        // CREAR JUGADOR NUEVO
        // -----------------------------------------------------

        SpawnearJugador();
    }

    // =========================================================
    // LIMPIEZA COMPLETA
    // =========================================================

    private void LimpiarEscenario()
    {
        // =====================================================
        // 1. ELIMINAR MODELOS DE ARMAS EQUIPADAS
        // =====================================================
        //
        // IMPORTANTE:
        // Los modelos equipados son Instantiate normal,
        // no PhotonNetwork.Instantiate.
        //
        // Por eso TODOS los clientes deben destruirlos
        // localmente.
        // =====================================================

        PlayerShooter[] shooters =
            FindObjectsByType<PlayerShooter>(
                FindObjectsSortMode.None
            );

        foreach (PlayerShooter shooter in shooters)
        {
            shooter.LimpiarArmaVisualLocal();
        }

        // =====================================================
        // 2. ELIMINAR PICKUPS DE ARMAS
        // =====================================================

        if (PhotonNetwork.IsMasterClient)
        {
            PickupArma[] armasSueltas =
                FindObjectsByType<PickupArma>(
                    FindObjectsSortMode.None
                );

            foreach (PickupArma arma in armasSueltas)
            {
                if (
                    arma != null &&
                    arma.photonView != null &&
                    arma.photonView.IsSceneView == false
                )
                {
                    PhotonNetwork.Destroy(
                        arma.gameObject
                    );
                }
            }
        }

        // =====================================================
        // 3. ELIMINAR TRAMPAS
        // =====================================================

        TrampaPlatano[] trampas =
            FindObjectsByType<TrampaPlatano>(
                FindObjectsSortMode.None
            );

        foreach (TrampaPlatano trampa in trampas)
        {
            if (
                trampa != null &&
                trampa.photonView != null &&
                trampa.photonView.IsMine
            )
            {
                PhotonNetwork.Destroy(
                    trampa.gameObject
                );
            }
        }

        // =====================================================
        // 4. ELIMINAR PROYECTILES
        // =====================================================

        ProyectilBasico[] balas =
            FindObjectsByType<ProyectilBasico>(
                FindObjectsSortMode.None
            );

        foreach (ProyectilBasico bala in balas)
        {
            if (
                bala != null &&
                bala.photonView != null &&
                bala.photonView.IsMine
            )
            {
                PhotonNetwork.Destroy(
                    bala.gameObject
                );
            }
        }

        // =====================================================
        // 5. REINICIAR GENERADORES DE ARMAS
        // =====================================================

        if (PhotonNetwork.IsMasterClient)
        {
            GeneradorArmas[] generadores =
                FindObjectsByType<GeneradorArmas>(
                    FindObjectsSortMode.None
                );

            foreach (
                GeneradorArmas generador
                in generadores
            )
            {
                generador.ReiniciarGenerador();
            }
        }
    }
}