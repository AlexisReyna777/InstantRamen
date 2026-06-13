using Unity.Netcode;
using UnityEngine;

public class ObjetoRecolectable : NetworkBehaviour
{
    private bool isPickUp = false;

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
        // Buscamos la caja y el jugador de forma segura en CUALQUIER cliente usando el SpawnManager global
        var objetosSincronizados = NetworkManager.Singleton.SpawnManager.SpawnedObjects;

        if (objetosSincronizados.TryGetValue(idCaja, out var netCaja) && 
            objetosSincronizados.TryGetValue(idPlayer, out var netPlayer))
        {
            PlayerMovement movimiento = netPlayer.GetComponent<PlayerMovement>();
            if (movimiento != null)
            {
                // Pasamos el GameObject de la caja al LateUpdate del jugador de forma local en cada pantalla
                movimiento.RecogerColectable(netCaja.gameObject);
            }
        }
    }
}