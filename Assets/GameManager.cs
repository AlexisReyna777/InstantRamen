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

    [Header("Puntos de Spawn")]
    // Aquí arrastraremos los Transforms de los objetos vacíos que creamos en el paso 1
    [SerializeField] private List<Transform> puntosDeSpawn;

    // Variables de Red, manejo de puntos
    public NetworkVariable<int> puntajeHost = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> puntajeCliente = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> tiempoRestante = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> juegoTerminado = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            tiempoRestante.Value = tiempoInicialPartida;
            juegoTerminado.Value = false;
            puntajeHost.Value = 0;
            puntajeCliente.Value = 0;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += AlCompletarCargaDeEscena;
        }
    }
    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= AlCompletarCargaDeEscena;
        }
    }

    private void AlCompletarCargaDeEscena(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
{
    if (!IsServer) return;

    // Ejecutamos la teletransportación con un mini retraso seguro de red
    StartCoroutine(TeletransportarJugadoresRetrasado(clientsCompleted));
}

    private System.Collections.IEnumerator TeletransportarJugadoresRetrasado(List<ulong> clientes)
    {
    yield return new WaitForEndOfFrame();

    Debug.Log("[SERVIDOR] Teletransportando jugadores de forma segura...");
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

                
                    playerObject.transform.position = posicionSpawn;
                    playerObject.transform.rotation = rotacionSpawn;

                    if (playerObject.TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out Unity.Netcode.Components.NetworkTransform netTransform))
                    {
                    
                        playerObject.transform.position = posicionSpawn; 
                    }

                    Debug.Log($"[SERVIDOR] Player ID {clientId} enviado con éxito al Spawn {index}");
                    index++;
                }
            }
        }
    }

    private void Update()
    {
        if (!IsServer) return; 
        if (juegoTerminado.Value) return;

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

    Debug.Log("[SERVIDOR] Red limpia. Recargando escena de juego...");

    NetworkManager.Singleton.SceneManager.LoadScene("MainScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
}
}
