using UnityEngine;
using Unity.Netcode;

public class TresEnRayaNetcode : NetworkBehaviour
{
    [Header("Componentes Visuales")]
    [Tooltip("Arrastra aquí los 9 GameObjects que actúan como casillas en orden (0 a 8)")]
    [SerializeField] private MeshRenderer[] casillasRenderers = new MeshRenderer[9];
    
    [Header("Materiales / Colores")]
    [SerializeField] private Material materialVacio;
    [SerializeField] private Material materialHost; // Representa la X
    [SerializeField] private Material materialCliente; // Representa la O

    // Una lista en red de 9 enteros. 0 = Vacío, 1 = Host, 2 = Cliente
    private NetworkList<int> estadoTablero;

    // Turno actual: 1 para el Host, 2 para el Cliente
    private NetworkVariable<int> turnoActual = new NetworkVariable<int>(1);

    private void Awake()
    {
        // Inicializamos la lista en red de tamaño 9
        estadoTablero = new NetworkList<int>(
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn()
    {
        estadoTablero.OnListChanged += AlCambiarTablero;
        ActualizarVisualesTablero();
    }

    public override void OnNetworkDespawn()
    {
        estadoTablero.OnListChanged -= AlCambiarTablero;
    }

    private void AlCambiarTablero(NetworkListEvent<int> changeEvent)
    {
        ActualizarVisualesTablero();
    }

    public void IntentarMarcarCasilla(int indiceCasilla)
    {
        MarcarCasillaServerRpc(indiceCasilla);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MarcarCasillaServerRpc(int indiceCasilla, ServerRpcParams rpcParams = default)
    {
        ulong idJugadorQuePresiono = rpcParams.Receive.SenderClientId;

        if (estadoTablero[indiceCasilla] != 0) return;

        int numeroJugador = (idJugadorQuePresiono == 0) ? 1 : 2;
        if (turnoActual.Value != numeroJugador) return;

        estadoTablero[indiceCasilla] = numeroJugador;
        turnoActual.Value = (turnoActual.Value == 1) ? 2 : 1;

        ComprobarGanador();
    }

    private void ActualizarVisualesTablero()
    {
        for (int i = 0; i < casillasRenderers.Length; i++)
        {
            if (casillasRenderers[i] == null) continue;

            if (estadoTablero[i] == 0) casillasRenderers[i].material = materialVacio;
            else if (estadoTablero[i] == 1) casillasRenderers[i].material = materialHost;
            else if (estadoTablero[i] == 2) casillasRenderers[i].material = materialCliente;
        }
    }

    private void ComprobarGanador()
    {
        int[][] combos = new int[][]
        {
            new int[] {0,1,2}, new int[] {3,4,5}, new int[] {6,7,8}, // Horizontales
            new int[] {0,3,6}, new int[] {1,4,7}, new int[] {2,5,8}, // Verticales
            new int[] {0,4,8}, new int[] {2,4,6}             // Diagonales
        };

        foreach (var combo in combos)
        {
            if (estadoTablero[combo[0]] != 0 &&
                estadoTablero[combo[0]] == estadoTablero[combo[1]] &&
                estadoTablero[combo[0]] == estadoTablero[combo[2]])
            {
                Invoke(nameof(ReiniciarTablero), 2f);
                return;
            }
        }

        bool empate = true;
        foreach (var casilla in estadoTablero)
        {
            if (casilla == 0) empate = false;
        }

        if (empate) Invoke(nameof(ReiniciarTablero), 2f);
    }

    private void ReiniciarTablero()
    {
        for (int i = 0; i < 9; i++) estadoTablero[i] = 0;
        turnoActual.Value = 1; 
    }
}