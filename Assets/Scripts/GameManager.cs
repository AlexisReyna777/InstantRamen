using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuración del Juego")]
    [SerializeField] private float tiempoInicialPartida = 180f;
    [SerializeField] private int jugadoresNecesariosParaEmpezar = 2; 

    [Header("Puntos de Spawn")]
    [SerializeField] private List<Transform> puntosDeSpawn;

    [Header("UI Cuenta Regresiva (NUEVO)")]
    [Tooltip("El componente Image de la UI que renderizará los números/GO")]
    [SerializeField] private Image imagenContadorUI;
    [Tooltip("Sprites en orden: Element 0 = Nulo, Element 1 = Sprite '1', Element 2 = Sprite '2', Element 3 = Sprite '3', Element 4 = Sprite 'GO'")]
    [SerializeField] private Sprite[] spritesContador = new Sprite[5];

    // Variables de Red
    public NetworkVariable<int> puntajeHost = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> puntajeCliente = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> tiempoRestante = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> juegoTerminado = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> partidaIniciada = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // NUEVO: Sincroniza qué imagen debe renderizarse en todos los clientes (0 = apagado, 1=1, 2=2, 3=3, 4=GO)
    private NetworkVariable<int> indiceImagenContadorNet = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // Bandera para evitar disparar la corrutina de inicio múltiples veces seguidas
    private bool cuentaRegresivaEnCurso = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        puntajeHost.OnValueChanged += AlCambiarPuntajeHost;
        puntajeCliente.OnValueChanged += AlCambiarPuntajeCliente;
        
        // NUEVO: Todos los clientes escuchan cuando cambia el índice para alternar las imágenes
        indiceImagenContadorNet.OnValueChanged += AlCambiarIndiceContador;
        ActualizarImagenUI(indiceImagenContadorNet.Value);

        if (IsServer)
        {
            tiempoRestante.Value = tiempoInicialPartida;
            juegoTerminado.Value = false;
            partidaIniciada.Value = false; 
            puntajeHost.Value = 0;
            puntajeCliente.Value = 0;
            indiceImagenContadorNet.Value = 0;
            cuentaRegresivaEnCurso = false;

            NetworkManager.Singleton.OnClientConnectedCallback += AlConectarseUnCliente;
            NetworkManager.Singleton.OnClientDisconnectCallback += AlDesconectarseUnCliente;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += AlCompletarCargaDeEscena;

            VerificarInicioDePartida();
        }
    }

    public override void OnNetworkDespawn()
    {
        puntajeHost.OnValueChanged -= AlCambiarPuntajeHost;
        puntajeCliente.OnValueChanged -= AlCambiarPuntajeCliente;
        indiceImagenContadorNet.OnValueChanged -= AlCambiarIndiceContador;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= AlConectarseUnCliente;
            NetworkManager.Singleton.OnClientDisconnectCallback -= AlDesconectarseUnCliente;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= AlCompletarCargaDeEscena;
        }
    }

    // --- NUEVAS FUNCIONES DE UI INTERNA ---
    private void AlCambiarIndiceContador(int valorViejo, int valorNuevo)
    {
        ActualizarImagenUI(valorNuevo);
    }

    private void ActualizarImagenUI(int indice)
    {
        if (imagenContadorUI == null) return;

        if (indice <= 0 || indice >= spritesContador.Length || spritesContador[indice] == null)
        {
            imagenContadorUI.gameObject.SetActive(false);
        }
        else
        {
            imagenContadorUI.sprite = spritesContador[indice];
            imagenContadorUI.gameObject.SetActive(true);
        }
    }

    private void AlCambiarPuntajeHost(int valorViejo, int valorNuevo)
    {
        ActualizarEscalaDeJugadorEnEscena(0, valorNuevo);
    }

    private void AlCambiarPuntajeCliente(int valorViejo, int valorNuevo)
    {
        ActualizarEscalaDeJugadorEnEscena(1, valorNuevo); 
    }

    private void ActualizarEscalaDeJugadorEnEscena(ulong targetClientId, int puntos)
    {
        PlayerMovement[] jugadores = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (PlayerMovement player in jugadores)
        {
            if ((targetClientId == 0 && player.OwnerClientId == 0) || (targetClientId == 1 && player.OwnerClientId > 0))
            {
                player.ActualizarEscalaPorPuntaje(puntos);
                break; 
            }
        }
    }

    private void AlConectarseUnCliente(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"[SERVIDOR] Se ha conectado el cliente ID: {clientId}");
        VerificarInicioDePartida();
    }

    private void AlDesconectarseUnCliente(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"[SERVIDOR] Se ha desconectado el cliente ID: {clientId}");
        
        if (NetworkManager.Singleton.ConnectedClientsIds.Count < jugadoresNecesariosParaEmpezar)
        {
            partidaIniciada.Value = false;
            cuentaRegresivaEnCurso = false;
            indiceImagenContadorNet.Value = 0;
            Debug.Log("[SERVIDOR] Jugadores insuficientes. Partida pausada/detenida.");
        }
    }

    private void VerificarInicioDePartida()
    {
        int jugadoresActuales = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"[SERVIDOR] Jugadores conectados actualmente: {jugadoresActuales}/{jugadoresNecesariosParaEmpezar}");

        // MODIFICADO: Si hay quórum, en vez de arrancar directo, ejecutamos la cuenta atrás de imágenes
        if (jugadoresActuales >= jugadoresNecesariosParaEmpezar && !partidaIniciada.Value && !cuentaRegresivaEnCurso)
        {
            Debug.Log("[SERVIDOR] ¡Quórum alcanzado! Lanzando secuencia arcade de imágenes...");
            StartCoroutine(RutinaCuentaRegresivaServidor());
        }
    }

    // NUEVO: Manejo secuencial de los sprites en red controlados por el Servidor
    private System.Collections.IEnumerator RutinaCuentaRegresivaServidor()
    {
        cuentaRegresivaEnCurso = true;
        partidaIniciada.Value = false; 

        // Mostrar Sprite "3"
        indiceImagenContadorNet.Value = 3;
        yield return new WaitForSeconds(1f);

        // Mostrar Sprite "2"
        indiceImagenContadorNet.Value = 2;
        yield return new WaitForSeconds(1f);

        // Mostrar Sprite "1"
        indiceImagenContadorNet.Value = 1;
        yield return new WaitForSeconds(1f);

        // Mostrar Sprite "GO" (Índice 4) y liberar el movimiento de los jugadores
        indiceImagenContadorNet.Value = 4;
        partidaIniciada.Value = true; 
        
        yield return new WaitForSeconds(1f);
        
        // Apagar la UI
        indiceImagenContadorNet.Value = 0; 
        cuentaRegresivaEnCurso = false;
    }

    private void AlCompletarCargaDeEscena(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        StartCoroutine(TeletransportarJugadoresRetrasado(clientsCompleted));
    }

    private System.Collections.IEnumerator TeletransportarJugadoresRetrasado(List<ulong> clientes)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        Debug.Log($"[SERVIDOR] Reubicando a {clientes.Count} jugadores basados en su ID de red...");

        foreach (ulong clientId in clientes)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient))
            {
                NetworkObject playerObject = networkClient.PlayerObject;
                if (playerObject != null)
                {
                    int spawnIndex = (clientId == 0) ? 0 : 1;

                    if (puntosDeSpawn == null || puntosDeSpawn.Count <= spawnIndex)
                    {
                        Debug.LogError($"[SERVIDOR] ¡Error Crítico! No hay suficientes puntos de spawn asignados en el GameManager para el jugador con ID {clientId}. Necesitas al menos {spawnIndex + 1} puntos.");
                        continue; 
                    }

                    Vector3 posicionSpawn = puntosDeSpawn[spawnIndex].position;
                    Quaternion rotacionSpawn = puntosDeSpawn[spawnIndex].rotation;

                    Debug.Log($"[SERVIDOR] Solicitando teletransporte seguro al script del jugador ID {clientId} hacia el punto #{spawnIndex}");

                    if (playerObject.TryGetComponent<PlayerMovement>(out var movimiento))
                    {
                        movimiento.TeletransportarASpawn(posicionSpawn, rotacionSpawn);
                    }
                    else
                    {
                        playerObject.transform.position = posicionSpawn;
                        playerObject.transform.rotation = rotacionSpawn;
                    }
                }
            }
        }

        // MODIFICADO: Cuando se recarga la escena tras reiniciar, relanzamos la cuenta regresiva en lugar de encender el juego de golpe
        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= jugadoresNecesariosParaEmpezar)
        {
            juegoTerminado.Value = false;
            if (!cuentaRegresivaEnCurso)
            {
                StartCoroutine(RutinaCuentaRegresivaServidor());
            }
        }
    }

    private void Update()
    {
        if (!IsServer) return; 
        if (!partidaIniciada.Value || juegoTerminado.Value) return;

        if (tiempoRestante.Value > 0)
        {
            tiempoRestante.Value -= Time.deltaTime;
        }
        else
        {
            tiempoRestante.Value = 0;
            TerminarPartida();
        }
    }

    public void SumarPuntosServer(ulong clientId, int puntos)
    {
        if (!IsServer) return;
        if (!partidaIniciada.Value || juegoTerminado.Value) return;

        if (clientId == 0) 
        {
            print("puntos sumados a host" + puntos);
            puntajeHost.Value += puntos;
        }
        else
        {
            print("puntos sumados a cliente" + puntos);
            puntajeCliente.Value += puntos;
        }
    }

    private void TerminarPartida()
    {
        juegoTerminado.Value = true;
        partidaIniciada.Value = false; 
        Debug.Log("[SERVIDOR] ¡Tiempo terminado! Fin de la partida.");
    }

    public void ReiniciarPartidaServer()
    {
        if (!IsServer) return;

        Debug.Log("[SERVIDOR] Iniciando limpieza de red previa al reinicio...");

        ObjetoRecolectable[] todosLosCubos = FindObjectsByType<ObjetoRecolectable>(FindObjectsSortMode.None);
        foreach (ObjetoRecolectable cubo in todosLosCubos)
        {
            if (cubo.TryGetComponent<NetworkObject>(out NetworkObject netObj))
            {
                if (netObj.IsSpawned)
                {
                    netObj.Despawn();
                    Destroy(cubo.gameObject);
                }
            }
        }

        puntajeHost.Value = 0;
        puntajeCliente.Value = 0;
        tiempoRestante.Value = tiempoInicialPartida;
        juegoTerminado.Value = false;
        partidaIniciada.Value = false; 
        indiceImagenContadorNet.Value = 0;
        cuentaRegresivaEnCurso = false;

        Debug.Log("[SERVIDOR] Red limpia. Recargando escena de juego...");
        NetworkManager.Singleton.SceneManager.LoadScene("MainScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}