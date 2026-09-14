using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Linq; // Necesario para ordenar la lista de jugadores

public class UIResultadosController : MonoBehaviour
{
    private GameObject panelPadre;
    private TMP_Text txtRonda;
    private TMP_Text txtGanador;
    private TMP_Text txtTablaPuntuaciones;
    private TMP_Text txtEstadoJugadores;
    private Button btnListo;
    private TMP_Text txtBotonListo;

    private Sprite spriteBorde;
    private readonly Color colTarjeta  = new Color(0.12f, 0.14f, 0.20f, 0.95f);
    private readonly Color colExito    = new Color(0.10f, 0.74f, 0.61f, 1.00f);
    private readonly Color colInactivo = new Color(0.25f, 0.28f, 0.38f, 1.00f);
    private readonly Color colDorada   = new Color(1.00f, 0.84f, 0.00f, 1.00f);

    private void Awake()
    {
        GenerarSpriteRedondeado();
        ConstruirUIProcedimental();
        panelPadre.SetActive(false);
    }

    private void GenerarSpriteRedondeado()
    {
        Texture2D tex = new Texture2D(32, 32);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        spriteBorde = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
    }

    private void ConstruirUIProcedimental()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas_Resultados");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        panelPadre = new GameObject("PanelResultados");
        panelPadre.transform.SetParent(canvas.transform, false);

        Image img = panelPadre.AddComponent<Image>();
        img.sprite = spriteBorde;
        img.type = Image.Type.Sliced;
        img.color = colTarjeta;

        RectTransform rt = panelPadre.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(850, 680); // Un poco más grande para que quepa la tabla

        VerticalLayoutGroup vlg = panelPadre.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 15;
        vlg.padding = new RectOffset(35, 35, 30, 30);

        // Textos de la UI
        txtRonda = CrearTexto(panelPadre.transform, "RONDA X", 22, colDorada, TextAlignmentOptions.Center, FontStyles.Bold);
        txtGanador = CrearTexto(panelPadre.transform, "¡VICTORIA!", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        txtTablaPuntuaciones = CrearTexto(panelPadre.transform, "", 20, Color.white, TextAlignmentOptions.Left);
        txtEstadoJugadores = CrearTexto(panelPadre.transform, "", 16, Color.gray, TextAlignmentOptions.Center);

        // Botón Listo
        GameObject btnGO = new GameObject("BtnListo");
        btnGO.transform.SetParent(panelPadre.transform, false);
        Image imgBtn = btnGO.AddComponent<Image>();
        imgBtn.sprite = spriteBorde;
        imgBtn.type = Image.Type.Sliced;
        imgBtn.color = colInactivo;

        btnListo = btnGO.AddComponent<Button>();
        // Asegúrate de que tienes instanciado tu script Luncher en la escena
        btnListo.onClick.AddListener(() => {
            if (Luncher.Instancia != null) 
                Luncher.Instancia.ToggleEstadoListoLocal();
        });

        RectTransform rtBtn = btnGO.GetComponent<RectTransform>();
        rtBtn.sizeDelta = new Vector2(380, 60);
        txtBotonListo = CrearTexto(btnGO.transform, "MARCAR COMO LISTO", 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    public void MostrarResultados(string ganador, int rondaActual, int maxRondas)
    {
        panelPadre.SetActive(true);

        if (rondaActual >= maxRondas)
        {
            txtRonda.text = "🏆 ¡PARTIDA FINALIZADA! 🏆";
            txtGanador.text = $"<color=#FFD700>¡PATO SUPREMO: {ganador}!</color>";
        }
        else
        {
            txtRonda.text = $"--- RONDA {rondaActual} DE {maxRondas} ---";
            txtGanador.text = $"Último en pie: <color=#FFD700>{ganador}</color>";
        }

        ActualizarTablaPuntuaciones();
        ActualizarEstadoJugadores();
    }

    private void ActualizarTablaPuntuaciones()
    {
        // Ordenamos a los jugadores leyendo sus puntos desde el ScoreManager
        var jugadoresOrdenados = PhotonNetwork.PlayerList
            .OrderByDescending(p => ScoreManager.ObtenerPuntos(p))
            .ToList();

        string[] motes = { "👑 PATO ALFA", "🦆 PATO DE GOMA", "🐟 PATO PESCADO", "🐣 PATO MAREADO" };
        string contenidoTabla = "<b>CLASIFICACIÓN (PUNTOS DE PLUMA):</b>\n\n";

        for (int i = 0; i < jugadoresOrdenados.Count; i++)
        {
            Player p = jugadoresOrdenados[i];
            int puntos = ScoreManager.ObtenerPuntos(p);
            string mote = (i < motes.Length) ? motes[i] : "🦆 PATO";

            // Resaltamos en amarillo al jugador local para que se encuentre rápido en la tabla
            string colorInicio = (p == PhotonNetwork.LocalPlayer) ? "<color=#FFD700>" : "<color=#FFFFFF>";
            string colorFin = "</color>";

            contenidoTabla += $"{colorInicio}{i + 1}. {p.NickName}  -  <b>{puntos} pts</b>  <i>({mote})</i>{colorFin}\n";
        }

        txtTablaPuntuaciones.text = contenidoTabla;
    }

    public void ActualizarEstadoJugadores()
    {
        txtEstadoJugadores.text = "<b>ESTADO SALA:</b> ";
        bool localEstaListo = false;

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            bool estaListo = p.CustomProperties.TryGetValue("IsReady", out object ready) && (bool)ready;
            if (p == PhotonNetwork.LocalPlayer) localEstaListo = estaListo;

            string estadoTexto = estaListo ? "<color=#10BB9B>[LISTO]</color>" : "<color=#E03F59>[ESPERANDO...]</color>";
            txtEstadoJugadores.text += $"{p.NickName} {estadoTexto}   ";
        }

        btnListo.image.color = localEstaListo ? colExito : colInactivo;
        txtBotonListo.text = localEstaListo ? "¡ESTÁS LISTO!" : "SIGUIENTE RONDA";
    }

    public void Ocultar()
    {
        panelPadre.SetActive(false);
    }

    private TMP_Text CrearTexto(Transform padre, string contenido, float tamano, Color color, TextAlignmentOptions alineacion, FontStyles estilo = FontStyles.Normal)
    {
        GameObject go = new GameObject("Texto");
        go.transform.SetParent(padre, false);
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = contenido;
        txt.fontSize = tamano;
        txt.color = color;
        txt.alignment = alineacion;
        txt.fontStyle = estilo;
        return txt;
    }
}