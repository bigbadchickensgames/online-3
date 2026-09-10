using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class UIResultadosController : MonoBehaviour
{
    private GameObject panelPadre;
    private TMP_Text txtGanador;
    private TMP_Text txtEstadoJugadores;
    private Button btnListo;
    private TMP_Text txtBotonListo;

    private Sprite spriteBorde;
    private readonly Color colTarjeta = new Color(0.12f, 0.14f, 0.20f, 0.95f);
    private readonly Color colExito   = new Color(0.10f, 0.74f, 0.61f, 1.00f);
    private readonly Color colInactivo= new Color(0.25f, 0.28f, 0.38f, 1.00f);

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
        rt.sizeDelta = new Vector2(800, 600);

        VerticalLayoutGroup vlg = panelPadre.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 20;
        vlg.padding = new RectOffset(40, 40, 40, 40);

        txtGanador = CrearTexto(panelPadre.transform, "¡VICTORIA DE JUGADOR!", 32, Color.yellow, TextAlignmentOptions.Center, FontStyles.Bold);
        txtEstadoJugadores = CrearTexto(panelPadre.transform, "", 20, Color.white, TextAlignmentOptions.Center);

        GameObject btnGO = new GameObject("BtnListo");
        btnGO.transform.SetParent(panelPadre.transform, false);

        Image imgBtn = btnGO.AddComponent<Image>();
        imgBtn.sprite = spriteBorde;
        imgBtn.type = Image.Type.Sliced;
        imgBtn.color = colInactivo;

        btnListo = btnGO.AddComponent<Button>();
        btnListo.onClick.AddListener(() => Luncher.Instancia.ToggleEstadoListoLocal());

        RectTransform rtBtn = btnGO.GetComponent<RectTransform>();
        rtBtn.sizeDelta = new Vector2(400, 70);

        txtBotonListo = CrearTexto(btnGO.transform, "MARCAR COMO LISTO", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    public void MostrarResultados(string ganador)
    {
        panelPadre.SetActive(true);
        txtGanador.text = $"¡RONDA FINALIZADA!\n<color=#FFD700>Ganador: {ganador}</color>";
        ActualizarEstadoJugadores();
    }

    public void ActualizarEstadoJugadores()
    {
        txtEstadoJugadores.text = "<b>ESTADO DE JUGADORES:</b>\n\n";

        bool localEstaListo = false;

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            bool estaListo = p.CustomProperties.TryGetValue("IsReady", out object ready) && (bool)ready;

            if (p == PhotonNetwork.LocalPlayer) localEstaListo = estaListo;

            string estadoTexto = estaListo ? "<color=#10BB9B>[LISTO]</color>" : "<color=#E03F59>[ESPERANDO...]</color>";
            txtEstadoJugadores.text += $"{p.NickName} : {estadoTexto}\n";
        }

        btnListo.image.color = localEstaListo ? colExito : colInactivo;
        txtBotonListo.text = localEstaListo ? "¡ESTÁS LISTO!" : "MARCAR COMO LISTO";
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