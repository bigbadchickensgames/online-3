using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class AutoLobbyManager : MonoBehaviourPunCallbacks
{
    private Canvas canvas;

    // Paneles principales
    private GameObject panelConexion;
    private GameObject panelSeleccionModo;
    private GameObject panelCrearPartida;
    private GameObject panelBuscarPartida;
    private GameObject panelSalaEspera;

    // Campos de Texto / Entrada
    private TMP_InputField inputNombreJugador;
    private TMP_InputField inputNombreSalaCrear;
    private TMP_InputField inputNombreSalaUnirse;
    private TMP_Text textoNombreSalaActual;
    private TMP_Text textoListaJugadores;
    private Transform contenedorSalas;
    private GameObject botonIniciarHost;

    // Paleta de Colores UI (Estilo Cyber-Dark Moderno)
    private readonly Color colTarjeta      = new Color(0.12f, 0.14f, 0.20f, 0.90f);
    private readonly Color colPrimario     = new Color(0.38f, 0.31f, 0.86f, 1.00f);
    private readonly Color colSecundario   = new Color(0.18f, 0.21f, 0.30f, 1.00f);
    private readonly Color colExito        = new Color(0.10f, 0.74f, 0.61f, 1.00f);
    private readonly Color colPeligro      = new Color(0.88f, 0.25f, 0.35f, 1.00f);
    private readonly Color colTextoPrincipal= new Color(0.95f, 0.96f, 0.98f, 1.00f);
    private readonly Color colTextoMutado  = new Color(0.55f, 0.60f, 0.70f, 1.00f);

    private Sprite spriteBordeRedondeado;
    private Dictionary<string, RoomInfo> listaSalasCache = new Dictionary<string, RoomInfo>();

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        GenerarSpriteRedondeado();
        ConstruirUIPorCodigo();
        MostrarPanel(panelConexion);
    }

    private void GenerarSpriteRedondeado()
    {
        Texture2D tex = new Texture2D(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        spriteBordeRedondeado = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
    }

    private void ConstruirUIPorCodigo()
    {
        GameObject canvasGO = new GameObject("Canvas_AutoLobby");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // --- PANELES PRINCIPALES (Ampliados para ocupar más pantalla) ---
        panelConexion = CrearTarjetaCentrada("PanelConexion", 1100, 650);
        panelSeleccionModo = CrearTarjetaCentrada("PanelSeleccionModo", 1100, 700);
        panelCrearPartida = CrearTarjetaCentrada("PanelCrearPartida", 1100, 700);
        panelBuscarPartida = CrearTarjetaCentrada("PanelBuscarPartida", 1300, 850);
        panelSalaEspera = CrearTarjetaCentrada("PanelSalaEspera", 1200, 800);

        // --- Panel 1: Conexión ---
        CrearTextoHeader(panelConexion.transform, "BIENVENIDO A LA RED", "Introduce tu nombre de usuario para continuar");
        inputNombreJugador = CrearInputField(panelConexion.transform, "Tu Nombre / Nickname...", 850, 75);
        CrearBotonEstilizado(panelConexion.transform, "CONECTAR Y ENTRAR", colPrimario, BotonConectar, 850, 75, 24);

        // --- Panel 2: Selección de Modo ---
        CrearTextoHeader(panelSeleccionModo.transform, "MENÚ PRINCIPAL", "Selecciona una modalidad de juego");
        CrearBotonEstilizado(panelSeleccionModo.transform, "CREAR PARTIDA (HOST)", colPrimario, () => MostrarPanel(panelCrearPartida), 900, 85, 22);
        CrearBotonEstilizado(panelSeleccionModo.transform, "BUSCAR PARTIDAS", colSecundario, () => MostrarPanel(panelBuscarPartida), 900, 85, 22);

        // --- Panel 3: Crear Partida ---
        CrearTextoHeader(panelCrearPartida.transform, "NUEVA SALA", "Asigna un nombre a tu partida multijugador");
        inputNombreSalaCrear = CrearInputField(panelCrearPartida.transform, "Nombre de la sala...", 900, 75);
        CrearBotonEstilizado(panelCrearPartida.transform, "CONFIRMAR Y CREAR", colPrimario, BotonConfirmarCrearSala, 900, 75, 22);
        CrearBotonEstilizado(panelCrearPartida.transform, "VOLVER", Color.clear, () => MostrarPanel(panelSeleccionModo), 500, 60, 18, colTextoMutado);

        // --- Panel 4: Buscar Partida ---
        CrearTextoHeader(panelBuscarPartida.transform, "EXPLORADOR DE SALAS", "Únete a una sala activa o introduce su nombre directo");

        GameObject filaBuscador = CrearFilaLayout(panelBuscarPartida.transform, 1150, 75);
        inputNombreSalaUnirse = CrearInputField(filaBuscador.transform, "Nombre exacto de sala...", 850, 70);
        CrearBotonEstilizado(filaBuscador.transform, "UNIRSE", colPrimario, BotonUnirsePorNombreDirecto, 260, 70, 20);

        GameObject scrollGO = new GameObject("ScrollView_Salas");
        scrollGO.transform.SetParent(panelBuscarPartida.transform, false);
        RectTransform rtScroll = scrollGO.AddComponent<RectTransform>();
        rtScroll.sizeDelta = new Vector2(1150, 420);

        Image imgScroll = scrollGO.AddComponent<Image>();
        imgScroll.sprite = spriteBordeRedondeado;
        imgScroll.type = Image.Type.Sliced;
        imgScroll.color = new Color(0.08f, 0.09f, 0.13f, 0.8f);

        ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        RectTransform rtVP = viewport.AddComponent<RectTransform>();
        rtVP.anchorMin = Vector2.zero; rtVP.anchorMax = Vector2.one;
        rtVP.sizeDelta = Vector2.zero;
        viewport.AddComponent<Image>();
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform rtContent = content.AddComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1); rtContent.anchorMax = Vector2.one;
        rtContent.pivot = new Vector2(0.5f, 1f);
        rtContent.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlgContent = content.AddComponent<VerticalLayoutGroup>();
        vlgContent.childControlWidth = true;
        vlgContent.childForceExpandWidth = true;
        vlgContent.spacing = 10;
        vlgContent.padding = new RectOffset(12, 12, 12, 12);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = rtContent;
        scrollRect.viewport = rtVP;
        contenedorSalas = content.transform;

        CrearBotonEstilizado(panelBuscarPartida.transform, "VOLVER AL MENÚ", Color.clear, () => MostrarPanel(panelSeleccionModo), 450, 60, 18, colTextoMutado);

        // --- Panel 5: Sala de Espera ---
        textoNombreSalaActual = CrearTextoHeader(panelSalaEspera.transform, "SALA: ---", "Esperando a que el host inicie la partida...").Item1;

        GameObject contenedorJugadores = new GameObject("ContenedorJugadores");
        contenedorJugadores.transform.SetParent(panelSalaEspera.transform, false);
        Image imgJug = contenedorJugadores.AddComponent<Image>();
        imgJug.sprite = spriteBordeRedondeado;
        imgJug.type = Image.Type.Sliced;
        imgJug.color = new Color(0.08f, 0.09f, 0.13f, 0.6f);

        RectTransform rtJug = contenedorJugadores.GetComponent<RectTransform>();
        rtJug.sizeDelta = new Vector2(1000, 320);

        textoListaJugadores = CrearTexto(contenedorJugadores.transform, "Cargando lista...", 22, colTextoPrincipal, TextAlignmentOptions.TopLeft);
        RectTransform rtTxtJug = textoListaJugadores.GetComponent<RectTransform>();
        rtTxtJug.anchorMin = Vector2.zero; rtTxtJug.anchorMax = Vector2.one;
        rtTxtJug.offsetMin = new Vector2(30, 30); rtTxtJug.offsetMax = new Vector2(-30, -30);

        botonIniciarHost = CrearBotonEstilizado(panelSalaEspera.transform, "INICIAR PARTIDA", colExito, BotonIniciarPartidaHost, 1000, 75, 22);
        CrearBotonEstilizado(panelSalaEspera.transform, "SALIR DE LA SALA", colPeligro, BotonSalirDeLaSala, 1000, 65, 20);
    }

    public void BotonConectar()
    {
        if (!string.IsNullOrEmpty(inputNombreJugador.text))
        {
            PhotonNetwork.NickName = inputNombreJugador.text;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster() => PhotonNetwork.JoinLobby();
    public override void OnJoinedLobby() => MostrarPanel(panelSeleccionModo);

    public void BotonConfirmarCrearSala()
    {
        string nombreSala = inputNombreSalaCrear.text;
        if (string.IsNullOrEmpty(nombreSala)) nombreSala = "Sala de " + PhotonNetwork.NickName;

        RoomOptions opciones = new RoomOptions { MaxPlayers = 4, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(nombreSala, opciones);
    }

    public void BotonUnirsePorNombreDirecto()
    {
        if (!string.IsNullOrEmpty(inputNombreSalaUnirse.text))
            PhotonNetwork.JoinRoom(inputNombreSalaUnirse.text);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList || !info.IsVisible || !info.IsOpen)
                listaSalasCache.Remove(info.Name);
            else
                listaSalasCache[info.Name] = info;
        }
        RenderizarListaSalas();
    }

    private void RenderizarListaSalas()
{
    // Limpiamos los elementos anteriores de la lista
    foreach (Transform child in contenedorSalas) Destroy(child.gameObject);

    foreach (var sala in listaSalasCache.Values)
    {
        string nombreSala = sala.Name;
        
        // Creamos el botón directamente en el contenedor pasándole un ancho real (1100px)
        GameObject btnSala = CrearBotonEstilizado(
            contenedorSalas, 
            $"{sala.Name}    [{sala.PlayerCount}/{sala.MaxPlayers}]", 
            colSecundario, 
            () => PhotonNetwork.JoinRoom(nombreSala), 
            1100, 60, 20
        );

        // Aseguramos que el Layout preserve la altura fija en la lista
        LayoutElement le = btnSala.AddComponent<LayoutElement>();
        le.preferredHeight = 60;
        le.minHeight = 60;
    }
}

    public override void OnJoinedRoom()
    {
        MostrarPanel(panelSalaEspera);
        textoNombreSalaActual.text = "SALA: " + PhotonNetwork.CurrentRoom.Name.ToUpper();
        ActualizarListaJugadores();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => ActualizarListaJugadores();
    public override void OnPlayerLeftRoom(Player otherPlayer) => ActualizarListaJugadores();

    private void ActualizarListaJugadores()
    {
        textoListaJugadores.text = "";
        foreach (Player jugador in PhotonNetwork.PlayerList)
        {
            string esHost = jugador.IsMasterClient ? "<color=#FFD700> [HOST]</color>" : "";
            textoListaJugadores.text += $"•  {jugador.NickName}{esHost}\n";
        }
        botonIniciarHost.SetActive(PhotonNetwork.IsMasterClient);
    }

    public void BotonIniciarPartidaHost()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.LoadLevel("Juego");
        }
    }

    public void BotonSalirDeLaSala() => PhotonNetwork.LeaveRoom();
    public override void OnLeftRoom() => MostrarPanel(panelSeleccionModo);

    private GameObject CrearTarjetaCentrada(string nombre, float ancho, float alto)
    {
        GameObject panel = new GameObject(nombre);
        panel.transform.SetParent(canvas.transform, false);

        Image img = panel.AddComponent<Image>();
        img.sprite = spriteBordeRedondeado;
        img.type = Image.Type.Sliced;
        img.color = colTarjeta;

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ancho, alto);

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 25;
        vlg.padding = new RectOffset(50, 50, 50, 50);

        return panel;
    }

    private GameObject CrearFilaLayout(Transform padre, float ancho, float alto)
    {
        GameObject fila = new GameObject("FilaHorizontal");
        fila.transform.SetParent(padre, false);

        RectTransform rt = fila.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(ancho, alto);

        HorizontalLayoutGroup hlg = fila.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 20;

        return fila;
    }

    private (TMP_Text, TMP_Text) CrearTextoHeader(Transform padre, string titulo, string subtitulo)
    {
        GameObject contenedor = new GameObject("HeaderGroup");
        contenedor.transform.SetParent(padre, false);
        
        RectTransform rtContenedor = contenedor.AddComponent<RectTransform>();
        rtContenedor.sizeDelta = new Vector2(1000, 100);

        VerticalLayoutGroup vlg = contenedor.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 6;

        TMP_Text txtTitulo = CrearTexto(contenedor.transform, titulo, 34, colTextoPrincipal, TextAlignmentOptions.Center, FontStyles.Bold);
        TMP_Text txtSub = CrearTexto(contenedor.transform, subtitulo, 17, colTextoMutado, TextAlignmentOptions.Center);

        return (txtTitulo, txtSub);
    }

    private GameObject CrearBotonEstilizado(Transform padre, string texto, Color colBase, UnityEngine.Events.UnityAction accion, float ancho, float alto, float tamanoTexto = 18, Color? colTexto = null)
    {
        GameObject go = new GameObject("Btn_" + texto);
        go.transform.SetParent(padre, false);

        Image img = go.AddComponent<Image>();
        img.sprite = spriteBordeRedondeado;
        img.type = Image.Type.Sliced;
        img.color = colBase;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(accion);

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(ancho, alto);

        Color cTexto = colTexto.HasValue ? colTexto.Value : colTextoPrincipal;
        TMP_Text txt = CrearTexto(go.transform, texto, tamanoTexto, cTexto, TextAlignmentOptions.Center, FontStyles.Bold);
        
        RectTransform rtTxt = txt.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;
        rtTxt.sizeDelta = Vector2.zero;

        return go;
    }

    private TMP_InputField CrearInputField(Transform padre, string placeholder, float ancho, float alto)
    {
        GameObject go = new GameObject("InputField");
        go.transform.SetParent(padre, false);

        Image img = go.AddComponent<Image>();
        img.sprite = spriteBordeRedondeado;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.08f, 0.09f, 0.13f, 0.9f);

        TMP_InputField input = go.AddComponent<TMP_InputField>();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(ancho, alto);

        TMP_Text textComp = CrearTexto(go.transform, "", 22, colTextoPrincipal, TextAlignmentOptions.Left);
        TMP_Text placeComp = CrearTexto(go.transform, placeholder, 22, colTextoMutado, TextAlignmentOptions.Left, FontStyles.Italic);

        RectTransform rtT = textComp.GetComponent<RectTransform>();
        rtT.anchorMin = Vector2.zero; rtT.anchorMax = Vector2.one;
        rtT.offsetMin = new Vector2(25, 0); rtT.offsetMax = new Vector2(-25, 0);

        RectTransform rtP = placeComp.GetComponent<RectTransform>();
        rtP.anchorMin = Vector2.zero; rtP.anchorMax = Vector2.one;
        rtP.offsetMin = new Vector2(25, 0); rtP.offsetMax = new Vector2(-25, 0);

        input.textComponent = textComp;
        input.placeholder = placeComp;

        return input;
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
        txt.textWrappingMode = TextWrappingModes.Normal;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        return txt;
    }

    private void MostrarPanel(GameObject panelActivo)
    {
        panelConexion.SetActive(panelActivo == panelConexion);
        panelSeleccionModo.SetActive(panelActivo == panelSeleccionModo);
        panelCrearPartida.SetActive(panelActivo == panelCrearPartida);
        panelBuscarPartida.SetActive(panelActivo == panelBuscarPartida);
        panelSalaEspera.SetActive(panelActivo == panelSalaEspera);
    }
}