using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GeneradorArmas : MonoBehaviourPun
{
    [Header("Armas Posibles para este Spawn")]
    public List<DatosArma> armasPosibles;

    [Header("Configuración")]
    public float tiempoReaparicion = 5f;

    private GameObject armaActualInstanciada;
    private bool esperandoReaparicion = false;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnArmaAleatoria();
        }
    }

    void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (armaActualInstanciada == null && !esperandoReaparicion)
        {
            StartCoroutine(RutinaReaparicion());
        }
    }

    private IEnumerator RutinaReaparicion()
    {
        esperandoReaparicion = true;
        yield return new WaitForSeconds(tiempoReaparicion);
        SpawnArmaAleatoria();
        esperandoReaparicion = false;
    }

    private void SpawnArmaAleatoria()
    {
        if (armasPosibles == null || armasPosibles.Count == 0) return;

        DatosArma elegida = armasPosibles[Random.Range(0, armasPosibles.Count)];

        if (elegida == null || string.IsNullOrEmpty(elegida.nombrePrefabModeloArma))
        {
            Debug.LogWarning("[GeneradorArmas] DatosArma o nombrePrefabModeloArma no está configurado.");
            return;
        }

        object[] datosInstanciacion = new object[] { elegida.name };

        armaActualInstanciada = PhotonNetwork.Instantiate(
            elegida.nombrePrefabModeloArma,
            transform.position,
            transform.rotation,
            0,
            datosInstanciacion
        );
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, new Vector3(0.6f, 0.5f, 0.6f));
    }
}