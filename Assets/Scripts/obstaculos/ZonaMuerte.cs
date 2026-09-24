using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Collider))]
public class ZonaMuerte : MonoBehaviour
{
    // Evita procesar dos veces el mismo objeto de red
    // (PhotonNetwork.Destroy se ejecuta al final del frame).
    private readonly HashSet<int> procesados = new HashSet<int>();

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void Awake()
    {
        Collider c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PhotonView view = other.GetComponentInParent<PhotonView>();

        // Objetos sin PhotonView (decoración local): se ignoran.
        if (view == null)
            return;

        // Solo el dueño del objeto actúa; así no se duplica en la red.
        if (!view.IsMine)
            return;

        if (!procesados.Add(view.ViewID))
            return;

        // ---------------- JUGADOR ----------------
        PlayerHealth salud = view.GetComponent<PlayerHealth>();

        if (salud != null)
        {
            salud.MatarPorVacio();
            return;
        }

        // ------- TRAMPAS, PROYECTILES, PICKUPS -------
        PhotonNetwork.Destroy(view.gameObject);
    }
}