using Unity.Netcode;
using UnityEngine;



public class ObjetoRecolectable : NetworkBehaviour
{
    private bool isPickUp = false;

    [Header("Configuración de Tipo")]
    [SerializeField] private bool esMaldito = false;
    [SerializeField] private float duracionEfectoMalo = 5.0f; // Segundos que durará invertido
    [SerializeField] private bool esCajaEncogedora = false; // Activa esto en el Inspector para la caja de encoger

    private void OnTriggerEnter(Collider other)
    {
        // 1. Solo el servidor procesa el choque físico e incrementa los puntos reales
        if (!IsServer) return;
        if (isPickUp) return;

        if (other.CompareTag("Player"))
        {
            isPickUp = true;

            // Desactivamos física y colisión en el servidor inmediatamente
            if (TryGetComponent<Collider>(out Collider col)) col.enabled = false; 
            if (TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = true;

            // --- ¡NUEVA LÍNEA PARA REGISTRAR LOS PUNTOS EN EL SERVIDOR! ---
            // Si tienes un script de estadísticas en el jugador, o en tu GameManager, se lo sumamos aquí.
            // Por ejemplo, si tu GameManager maneja los puntos de la partida:
            if (GameManager.Instance != null)
            {
                // Aquí puedes llamar a tu función de sumar puntos. Ejemplo:
                // GameManager.Instance.SumarPuntoServer(other.GetComponent<NetworkObject>().OwnerClientId);
            }

            // Obtenemos los IDs únicos de red para la parte visual
            ulong idRedCaja = GetComponent<NetworkObject>().NetworkObjectId;
            ulong idRedPlayer = other.GetComponent<NetworkObject>().NetworkObjectId;

            // Le ordenamos a todas las pantallas que hagan el apilamiento visual
            AcomodarCajaEnClienteClientRpc(idRedCaja, idRedPlayer);
        }
    }

    [ClientRpc]
    private void AcomodarCajaEnClienteClientRpc(ulong idCaja, ulong idPlayer)
    {
        var objetosSincronizados = NetworkManager.Singleton.SpawnManager.SpawnedObjects;

        if (objetosSincronizados.TryGetValue(idCaja, out var netCaja) && 
            objetosSincronizados.TryGetValue(idPlayer, out var netPlayer))
        {
            PlayerMovement movimiento = netPlayer.GetComponent<NetworkObject>().GetComponent<PlayerMovement>();
            if (movimiento != null)
            {
                // Pasamos la caja al LateUpdate para el apilamiento visual
                movimiento.RecogerColectable(netCaja.gameObject);

                // --- NUEVO: SI LA CAJA ES MALDITA, INVERTIMOS LOS CONTROLES ---
                if (esMaldito)
                {
                    movimiento.AplicarEfectoInversion(duracionEfectoMalo);
                }
                if (esCajaEncogedora)
                {
                    movimiento.AplicarEfectoEncoger(duracionEfectoMalo); // Reutilizamos la misma variable de duración
                }
            }
        }
    }
}