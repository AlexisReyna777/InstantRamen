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
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= AlConectarseUnCliente;
            NetworkManager.Singleton.OnClientDisconnectCallback -= AlDesconectarseUnCliente;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= AlCompletarCargaDeEscena;
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
        // Contamos cuántos dispositivos están conectados al NetworkManager actualmente
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
        // Esperamos a que todo se asiente en la escena recargada
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
                    // ---- ASIGNACIÓN DE SPAWN INTELIGENTE POR ID ----
                    // Si el ID es 0, es el Host (usa el punto 0). 
                    // Si es cualquier otro número, es el Cliente (usa el punto 1).
                    int spawnIndex = (clientId == 0) ? 0 : 1;

                    // Control de seguridad por si olvidaste poner los 2 puntos en el Inspector
                    if (puntosDeSpawn == null || puntosDeSpawn.Count <= spawnIndex)
                    {
                        Debug.LogError($"[SERVIDOR] ¡Error Crítico! No hay suficientes puntos de spawn asignados en el GameManager para el jugador con ID {clientId}. Necesitas al menos {spawnIndex + 1} puntos.");
                        continue; 
                    }

                    Vector3 posicionSpawn = puntosDeSpawn[spawnIndex].position;
                    Quaternion rotacionSpawn = puntosDeSpawn[spawnIndex].rotation;

                    Debug.Log($"[SERVIDOR] Asignando Punto de Spawn #{spawnIndex} al jugador con ID {clientId}. Posición: {posicionSpawn}");

                    // 1. Forzamos el teletransporte nativo de Netcode si el objeto usa NetworkTransform
                    if (playerObject.TryGetComponent<NetworkTransform>(out var netTransform))
                    {
                        netTransform.Teleport(posicionSpawn, rotacionSpawn, playerObject.transform.localScale);
                    }
                    
                    // 2. Forzamos el RPC en la pantalla del cliente para que su dueño acepte el cambio
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
        
        // Si la partida NO inició, o ya terminó, congelamos el tiempo
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
        // Evitamos sumar puntos si alguien hace trampa antes de que se conecten todos
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
        partidaIniciada.Value = false; // Apagamos el flag de juego activo
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
        
        // APAGAMOS temporalmente la partida mientras carga la escena
        partidaIniciada.Value = false; 

        Debug.Log("[SERVIDOR] Red limpia. Recargando escena de juego...");
        NetworkManager.Singleton.SceneManager.LoadScene("MainScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}