using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI; // Asegúrate de importar esto para la UI tradicional, o TMPro si usas TextMeshPro

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuración del Juego")]
    [SerializeField] private float tiempoInicialPartida = 180f; 

    // Variables de Red sincronizadas automáticamente
    // Guardamos el puntaje del Jugador 0 (Host) y Jugador 1 (Cliente 1) de forma simple para cumplir el MVP
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
        }
    }

    private void Update()
    {
        if (!IsServer) return; // Solo el servidor descuenta el tiempo
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

    // El script ZonaEntrega llamará a esta función en el servidor
    public void SumarPuntosServer(ulong clientId, int puntos)
    {
        if (!IsServer) return;

        // Estructura simple recomendada por el enunciado (Puntaje J1, J2, etc.)
        if (clientId == 0) // El Host suele ser el ID 0
        {
            puntajeHost.Value += puntos;
        }
        else
        {
            puntajeCliente.Value += puntos; // Suma al primer cliente conectado
        }
    }

    private void TerminarPartida()
    {
        juegoTerminado.Value = true;
        Debug.Log("[SERVIDOR] ¡Tiempo terminado! Fin de la partida.");
        // Aquí se activará la pantalla de ganador en la fase de UI
    }
}
