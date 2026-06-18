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

    // Variables de Red
    public NetworkVariable<int> puntajeHost = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> puntajeCliente = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> tiempoRestante = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> juegoTerminado = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> partidaIniciada = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // --- NUEVO: Todos los clientes escuchan cuando los puntajes cambian ---
        puntajeHost.OnValueChanged += AlCambiarPuntajeHost;
        puntajeCliente.OnValueChanged += AlCambiarPuntajeCliente;

        if (IsServer)
        {
            // Al nacer el GameManager, seteamos todo en "espera"
            tiempoRestante.Value = tiempoInicialPartida;
            juegoTerminado.Value = false;
            partidaIniciada.Value = false; 
            puntajeHost.Value = 0;
            puntajeCliente.Value = 0;

            // Nos suscribimos a los eventos de Netcode para saber cuando entra o sale un player
            NetworkManager.Singleton.OnClientConnectedCallback += AlConectarseUnCliente;
            NetworkManager.Singleton.OnClientDisconnectCallback += AlDesconectarseUnCliente;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += AlCompletarCargaDeEscena;

            // Chequeo rápido por si el Host entró antes de que este script se suscribiera
            VerificarInicioDePartida();
        }
    }

    public override void OnNetworkDespawn()
    {
        // --- NUEVO: Desuscripción de eventos de variables de red ---
        puntajeHost.OnValueChanged -= AlCambiarPuntajeHost;
        puntajeCliente.OnValueChanged -= AlCambiarPuntajeCliente;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= AlConectarseUnCliente;
            NetworkManager.Singleton.OnClientDisconnectCallback -= AlDesconectarseUnCliente;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= AlCompletarCargaDeEscena;
        }
    }

    // --- NUEVAS FUNCIONES: Detectan cambios de puntos en red y actualizan escalas ---
    private void AlCambiarPuntajeHost(int valorViejo, int valorNuevo)
    {
        ActualizarEscalaDeJugadorEnEscena(0, valorNuevo);
    }

    private void AlCambiarPuntajeCliente(int valorViejo, int valorNuevo)
    {
        // Buscamos cualquier ID que no sea 0 (el cliente)
        ActualizarEscalaDeJugadorEnEscena(1, valorNuevo); 
    }

    private void ActualizarEscalaDeJugadorEnEscena(ulong targetClientId, int puntos)
    {
        // Buscamos los jugadores en la escena local de este cliente/host
        PlayerMovement[] jugadores = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (PlayerMovement player in jugadores)
        {
            // Si el ID coincide (Host es 0, Cliente es mayor a 0)
            if ((targetClientId == 0 && player.OwnerClientId == 0) || (targetClientId == 1 && player.OwnerClientId > 0))
            {
                player.ActualizarEscalaPorPuntaje(puntos);
                break; // Ya encontramos al jugador que buscábamos
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
        
        // Si el cliente se va en medio de la partida, pausamos el juego
        if (NetworkManager.Singleton.ConnectedClientsIds.Count < jugadoresNecesariosParaEmpezar)
        {
            partidaIniciada.Value = false;
            Debug.Log("[SERVIDOR] Jugadores insuficientes. Partida pausada/detenida.");
        }
    }

    private void VerificarInicioDePartida()
    {
        int jugadoresActuales = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"[SERVIDOR] Jugadores conectados actualmente: {jugadoresActuales}/{jugadoresNecesariosParaEmpezar}");

        if (jugadoresActuales >= jugadoresNecesariosParaEmpezar && !partidaIniciada.Value)
        {
            Debug.Log("[SERVIDOR] ¡Quórum alcanzado! La partida comienza AHORA.");
            partidaIniciada.Value = true;
        }
    }

    private void AlCompletarCargaDeEscena(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;
        // Se dispara al iniciar por primera vez y también al reiniciar la escena de juego
        StartCoroutine(TeletransportarJugadoresRetrasado(clientsCompleted));
    }

    private System.Collections.IEnumerator TeletransportarJugadoresRetrasado(List<ulong> clientes)
    {
        // Esperamos a que todo se asiente en la escena recargada para evitar tirones
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
                    // Asignación de index de Spawn por ID de cliente
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
                        // Respaldo de seguridad solo si el objeto no tiene el script PlayerMovement
                        playerObject.transform.position = posicionSpawn;
                        playerObject.transform.rotation = rotacionSpawn;
                    }
                }
            }
        }

        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= jugadoresNecesariosParaEmpezar)
        {
            partidaIniciada.Value = true;
            juegoTerminado.Value = false;
            Debug.Log("[SERVIDOR] ¡Todos los jugadores en sus marcas! Partida iniciada de forma segura.");
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
            puntajeHost.Value += puntos;
        }
        else
        {
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

        Debug.Log("[SERVIDOR] Red limpia. Recargando escena de juego...");
        NetworkManager.Singleton.SceneManager.LoadScene("MainScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}