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
    [SerializeField] private int jugadoresNecesariosParaEmpezar = 2; // ¡NUEVO!

    [Header("Puntos de Spawn")]
    [SerializeField] private List<Transform> puntosDeSpawn;

    // Variables de Red
    public NetworkVariable<int> puntajeHost = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> puntajeCliente = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> tiempoRestante = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> juegoTerminado = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // ¡NUEVO!: Variable de red para avisarle a la UI si la partida realmente empezó
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
            partidaIniciada.Value = false; // El juego NO empieza todavía
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
        
        // Opcional: Si el cliente se va en medio de la partida, podrías pausar o terminar el juego
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
        StartCoroutine(TeletransportarJugadoresRetrasado(clientsCompleted));
    }

    private System.Collections.IEnumerator TeletransportarJugadoresRetrasado(List<ulong> clientes)
    {
        yield return new WaitForEndOfFrame();
        int index = 0;
        foreach (ulong clientId in clientes)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient))
            {
                NetworkObject playerObject = networkClient.PlayerObject;
                if (playerObject != null && puntosDeSpawn.Count > index)
                {
                    Vector3 posicionSpawn = puntosDeSpawn[index].position;
                    Quaternion rotacionSpawn = puntosDeSpawn[index].rotation;

                    if (playerObject.TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out var netTransform))
                    {
                        netTransform.Teleport(posicionSpawn, rotacionSpawn, playerObject.transform.localScale);
                    }
                    else
                    {
                        playerObject.transform.position = posicionSpawn;
                        playerObject.transform.rotation = rotacionSpawn;
                    }
                    index++;
                }
            }
        }
    }

    private void Update()
    {
        if (!IsServer) return; 
        
        // ¡EL ESCUDO DEL UPDATE!: Si la partida NO inició, o ya terminó, congelamos el tiempo
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
        partidaIniciada.Value = false; // Volvemos a esperar a que carguen de nuevo

        Debug.Log("[SERVIDOR] Red limpia. Recargando escena de juego...");
        NetworkManager.Singleton.SceneManager.LoadScene("MainScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}