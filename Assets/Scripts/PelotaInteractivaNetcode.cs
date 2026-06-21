using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class PelotaInteractivaNetcode : NetworkBehaviour
{
    [Header("Configuración de Respawn")]
    [Tooltip("Si la pelota cae por debajo de esta altura (Eje Y), respawneará")]
    [SerializeField] private float alturaLimiteMuerte = -10f;

    private Rigidbody rb;
    private Vector3 posicionInicial;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Guardamos la posición exacta donde pusiste la pelota en la escena al iniciar
        posicionInicial = transform.position;
    }

    // El cliente llama a esto cuando hace clic y empieza a arrastrar
    public void IniciarArrastreLocal()
    {
        SolicitarPropiedadRpc();
    }

    // El cliente llama a esto cuando suelta el mouse para lanzarla
    public void LanzarPelotaLocal(Vector3 fuerzaLanzamiento)
    {
        LanzarPelotaRpc(fuerzaLanzamiento);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarPropiedadRpc(RpcParams rpcParams = default)
    {
        ulong clienteId = rpcParams.Receive.SenderClientId;
        
        NetworkObject.ChangeOwnership(clienteId);
        
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void LanzarPelotaRpc(Vector3 fuerza, RpcParams rpcParams = default)
    {
        rb.isKinematic = false;
        rb.useGravity = true;
        
        rb.AddForce(fuerza, ForceMode.Impulse);
    }

    // NUEVO: El servidor monitorea constantemente la posición de la pelota
    private void Update()
    {
        // Solo el servidor tiene la autoridad de evaluar y ejecutar el respawn
        if (!IsServer) return;

        // Si la pelota cae por debajo del límite establecido...
        if (transform.position.y < alturaLimiteMuerte)
        {
            RespawnearPelota();
        }
    }

    private void RespawnearPelota()
    {
        Debug.Log("[SERVIDOR] La pelota se salió del mapa. Respawnear de vuelta al origen.");

        // 1. Frenamos por completo cualquier fuerza o rotación acumulada
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 2. Teletransportamos el transform en el servidor (Netcode replicará esto a todos los clientes automáticamente)
        transform.position = posicionInicial;
        transform.rotation = Quaternion.identity;
    }
}