using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

public class GeneradorArmas : MonoBehaviourPun
{
    [Header("Armas Posibles para este Spawn")]
    public List<DatosArma> armasPosibles;

    [Header("Configuración")]
    public float tiempoReaparicion = 5f;

    private GameObject armaActualInstanciada;
    private bool esperandoReaparicion = false;

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnArmaAleatoria();
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (
            armaActualInstanciada == null &&
            !esperandoReaparicion
        )
        {
            StartCoroutine(
                RutinaReaparicion()
            );
        }
    }

    // =========================================================
    // REAPARICION NORMAL
    // =========================================================

    private IEnumerator RutinaReaparicion()
    {
        esperandoReaparicion = true;

        yield return new WaitForSeconds(
            tiempoReaparicion
        );

        // Por seguridad, comprobar otra vez
        // que sigue sin haber arma.
        if (armaActualInstanciada == null)
        {
            SpawnArmaAleatoria();
        }

        esperandoReaparicion = false;
    }

    // =========================================================
    // SPAWN DEL ARMA
    // =========================================================

    private void SpawnArmaAleatoria()
    {
        if (
            armasPosibles == null ||
            armasPosibles.Count == 0
        )
        {
            return;
        }

        // Evitar duplicados
        if (armaActualInstanciada != null)
        {
            return;
        }

        DatosArma elegida =
            armasPosibles[
                Random.Range(
                    0,
                    armasPosibles.Count
                )
            ];

        if (
            elegida == null ||
            string.IsNullOrEmpty(
                elegida.nombrePrefabModeloArma
            )
        )
        {
            Debug.LogWarning(
                "[GeneradorArmas] DatosArma o nombrePrefabModeloArma no está configurado."
            );

            return;
        }

        object[] datosInstanciacion =
        {
            elegida.name
        };

        armaActualInstanciada =
            PhotonNetwork.Instantiate(
                elegida.nombrePrefabModeloArma,
                transform.position,
                transform.rotation,
                0,
                datosInstanciacion
            );
    }

    // =========================================================
    // REINICIO DE RONDA
    // =========================================================

    public void ReiniciarGenerador()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // Detener cualquier coroutine de respawn pendiente
        StopAllCoroutines();

        esperandoReaparicion = false;

        // Si queda un arma registrada, destruirla.
        if (armaActualInstanciada != null)
        {
            if (
                armaActualInstanciada.TryGetComponent
                <PhotonView>(out PhotonView pv)
            )
            {
                if (pv.IsMine)
                {
                    PhotonNetwork.Destroy(
                        armaActualInstanciada
                    );
                }
            }

            armaActualInstanciada = null;
        }

        // Buscar también cualquier PickupArma
        // que pertenezca exactamente a este generador.
        //
        // Esto sirve como red de seguridad.
        PickupArma[] pickups =
            FindObjectsByType<PickupArma>(
                FindObjectsSortMode.None
            );

        foreach (PickupArma pickup in pickups)
        {
            if (pickup == null)
                continue;

            if (
                Vector3.Distance(
                    pickup.transform.position,
                    transform.position
                ) < 0.2f
            )
            {
                if (
                    pickup.photonView != null &&
                    pickup.photonView.IsMine
                )
                {
                    PhotonNetwork.Destroy(
                        pickup.gameObject
                    );
                }
            }
        }

        // Crear inmediatamente un arma nueva
        SpawnArmaAleatoria();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(
            transform.position +
            Vector3.up * 0.25f,
            new Vector3(
                0.6f,
                0.5f,
                0.6f
            )
        );
    }
}