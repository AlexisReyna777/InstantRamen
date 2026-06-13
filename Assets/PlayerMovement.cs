using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components; // IMPORTANTE: Necesario para detectar el NetworkTransform
using System.Collections;
using System.Collections.Generic; // REQUERIDO: Para usar listas genéricas de C#

public class PlayerMovement : NetworkBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float speed = 5f; // Funciona como tu velocidad máxima base
    
    [Header("Mecánica de Peso y Apilamiento")]
    [SerializeField] private Transform puntoAnclajeCajas; // Un objeto vacío en la espalda/cabeza del player
    [SerializeField] private Vector3 dimensionesCaja = new Vector3(0f, 0.5f, 0f); // Cuánto mide de alto la caja para sumar en el eje Y
    [SerializeField] private Vector3 rotacionCajaApilada = new Vector3(0f, 90f, 0f);
    [SerializeField] private float penalizacionPorObjeto = 0.5f; // Cuánto frena cada caja encima
    [SerializeField] private float velocidadMinima = 1.5f; // Límite para no quedarse totalmente quieto

    [Header("Modelos Visuales")]
    [SerializeField] private GameObject modeloHost;
    [SerializeField] private GameObject modeloCliente;

    [Header("Avatars Específicos")]
    [SerializeField] private Avatar avatarHost;
    [SerializeField] private Avatar actorCliente; // Nota: Mantenido por si está mapeado en el Inspector
    [SerializeField] private Avatar avatarCliente;
    
    private Animator miAnimator;
    
    // MODIFICADO: Ahora es una propiedad pública con 'private set' para que ZonaEntrega lea las cajas actuales
    public int colectablesActuales { get; private set; } = 0;
    private float velocidadModificada;

    // Lista local e independiente en cada cliente para actualizar la posición visual sin alterar la jerarquía de red
    private List<GameObject> cajasCargadasVisuales = new List<GameObject>();

    // NUEVO MÉTODO: Permite a ZonaEntrega acceder a las referencias de las cajas para eliminarlas de la red
    public List<GameObject> ObtenerCajasCargadas() 
    { 
        return cajasCargadasVisuales; 
    }

    private void Awake()
    {
        miAnimator = GetComponent<Animator>();
        velocidadModificada = speed; // Al inicio arranca al 100% de la velocidad base
    }

    public override void OnNetworkSpawn()
    {
        ActualizarVisualesYAvatar();

        if (IsOwner)
        {
            Renderer rendererHijo = GetComponentInChildren<Renderer>();
            if (rendererHijo != null)
            {
                rendererHijo.material.color = Color.red;
            }
        }
    }

    private void ActualizarVisualesYAvatar()
    {
        if (miAnimator == null) miAnimator = GetComponent<Animator>();

        if (OwnerClientId == 0) // El Host
        {
            if (modeloHost != null) modeloHost.SetActive(true);
            if (modeloCliente != null) modeloCliente.SetActive(false);
            if (miAnimator != null && avatarHost != null) miAnimator.avatar = avatarHost;
        }
        else // El Cliente
        {
            if (modeloHost != null) modeloHost.SetActive(false);
            if (modeloCliente != null) modeloCliente.SetActive(true);
            if (miAnimator != null && avatarCliente != null) miAnimator.avatar = avatarCliente;
        }
    }

    void Update()
    {
        if (!IsOwner) return;
        if (GameManager.Instance != null && !GameManager.Instance.partidaIniciada.Value) return;

        float temporalX = Input.GetAxisRaw("Horizontal");
        float temporalZ = Input.GetAxisRaw("Vertical");

        float inputX = 0f;
        float inputZ = 0f;

        if (Mathf.Abs(temporalX) >= Mathf.Abs(temporalZ))
        {
            inputX = temporalX;
            inputZ = 0f;
        }
        else
        {
            inputX = 0f;
            inputZ = temporalZ;
        }

        Vector3 move = new Vector3(inputX, 0f, inputZ);
        float velocidadActual = move.magnitude;

        // ANIMACIÓN CONTROLADA POR RED MANUAL
        if (miAnimator != null)
        {
            miAnimator.SetFloat("Velocidad", velocidadActual);
        }
        
        SincronizarAnimacionServerRpc(velocidadActual);

        if (move.magnitude > 0.1f)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(move);
            if (Quaternion.Angle(transform.rotation, rotacionObjetivo) > 0.1f)
            {
                transform.rotation = rotacionObjetivo;
            }
            
            // Multiplica por velocidadModificada, aplicando el peso dinámico
            transform.position += move * velocidadModificada * Time.deltaTime;
        }
    }

    // Se ejecuta después del Update para evitar desfases o temblores visuales (jittering) al caminar
    private void LateUpdate()
    {
        if (puntoAnclajeCajas == null || cajasCargadasVisuales.Count == 0) return;

        // Cada frame, posicionamos y rotamos visualmente las cajas sobre el punto de anclaje de forma manual
        for (int i = 0; i < cajasCargadasVisuales.Count; i++)
        {
            if (cajasCargadasVisuales[i] == null) continue;

            // Calculamos el desplazamiento vertical acumulado por cada piso de cajas
            Vector3 desfaceAltura = dimensionesCaja * i;

            // Posición en el mundo = posición del anclaje + desplazamiento rotado según la orientación del jugador
            cajasCargadasVisuales[i].transform.position = puntoAnclajeCajas.position + (puntoAnclajeCajas.rotation * desfaceAltura);
            
            // Rotación fija combinando el giro del jugador con la inclinación deseada (ej: acostada)
            cajasCargadasVisuales[i].transform.rotation = puntoAnclajeCajas.rotation * Quaternion.Euler(rotacionCajaApilada);
        }
    }

    [ServerRpc]
    private void SincronizarAnimacionServerRpc(float velocidad)
    {
        SincronizarAnimacionClientRpc(velocidad);
    }

    [ClientRpc]
    private void SincronizarAnimacionClientRpc(float velocidad)
    {
        if (IsOwner) return;

        if (miAnimator != null)
        {
            miAnimator.SetFloat("Velocidad", velocidad);
        }
    }

    // --- SISTEMA DE PESO, REDUCCIÓN Y LIMPIEZA ---

    public void RecogerColectable(GameObject objetoCaja)
    {
        // 1. Desactivamos el efecto flotante/giratorio de la caja localmente
        if (objetoCaja.TryGetComponent<EfectoColectable>(out var efecto))
        {
            efecto.DesactivarEfectoYRotar(Quaternion.Euler(rotacionCajaApilada));
        }

        // 2. Registramos la caja en la lista del LateUpdate para su seguimiento visual continuo
        if (!cajasCargadasVisuales.Contains(objetoCaja))
        {
            cajasCargadasVisuales.Add(objetoCaja);
        }

        // 3. Incrementamos el contador en todas las pantallas para que la simulación de velocidad coincida
        colectablesActuales++;
        RecalcularVelocidad();
    }

    // Método que llama la Zona de Entrega a través del servidor
    public void EntregarColectablesDesdeServidor()
    {
        if (!IsServer) return;
        LimpiarPesoClientRpc();
    }

    [ClientRpc]
    private void LimpiarPesoClientRpc()
    {
        // CORREGIDO: Todos los clientes limpian las cajas visuales de sus mundos
        cajasCargadasVisuales.Clear();

        // CORREGIDO: Se removió el candado IsOwner para que todos los clientes pongan a cero este personaje específico
        colectablesActuales = 0; 
        RecalcularVelocidad();
        
        Debug.Log($"[Netcode] Peso y velocidad restablecidos en el cliente para el jugador ID: {OwnerClientId}");
    }

    private void RecalcularVelocidad()
    {
        // Restamos el peso acumulado de la velocidad máxima original (speed)
        float calculoNuevaVelocidad = speed - (colectablesActuales * penalizacionPorObjeto);
        
        // Protegemos que no sea inferior a la velocidad mínima para que el player se mueva
        velocidadModificada = Mathf.Max(calculoNuevaVelocidad, velocidadMinima);

        Debug.Log($"[PESO LOG] Player ID: {OwnerClientId} | Cajas: {colectablesActuales} | Velocidad resultante: {velocidadModificada}m/s");
    }

    // --- CORRECCIÓN DEL TELETRANSPORTE PARA EVITAR EXCEPCIONES DE AUTORIDAD ---

    public void TeletransportarASpawn(Vector3 nuevaPosicion, Quaternion nuevaRotacion)
    {
        if (IsServer)
        {
            // NUEVO: El servidor fuerza de inmediato el reseteo de peso al reiniciar/teletransportar
            LimpiarPesoClientRpc();

            NetworkTransform netTransform = GetComponent<NetworkTransform>();
            
            if (netTransform != null && netTransform.CanCommitToTransform)
            {
                netTransform.Teleport(nuevaPosicion, nuevaRotacion, transform.localScale);
            }
            else
            {
                transform.position = nuevaPosicion;
                transform.rotation = nuevaRotacion;
            }

            CambiarPosicionClientRpc(nuevaPosicion, nuevaRotacion);
        }
    }

    [ClientRpc]
    private void CambiarPosicionClientRpc(Vector3 pos, Quaternion rot)
    {
        if (IsServer) return;

        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        
        if (netTransform != null)
        {
            netTransform.enabled = false;
            transform.position = pos;
            transform.rotation = rot;
            netTransform.enabled = true;
        }
        else
        {
            transform.position = pos;
            transform.rotation = rot;
        }
        
        Debug.Log($"[Netcode] Posición de reinicio sincronizada en el cliente de forma segura: {pos}");
    }
}