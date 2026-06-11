using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components; // IMPORTANTE: Necesario para detectar el NetworkTransform
using System.Collections;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float speed = 5f;
    
    [Header("Modelos Visuales")]
    [SerializeField] private GameObject modeloHost;
    [SerializeField] private GameObject modeloCliente;

    [Header("Avatars Específicos")]
    [SerializeField] private Avatar avatarHost;
    [SerializeField] private Avatar actorCliente; // Nota: Puedes mantener el nombre exacto de tu variable original si lo deseas
    [SerializeField] private Avatar avatarCliente;
    
    private Animator miAnimator;

    private void Awake()
    {
        miAnimator = GetComponent<Animator>();
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
            transform.position += move * speed * Time.deltaTime;
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

    // --- CORRECCIÓN DEL TELETRANSPORTE PARA EVITAR EXCEPCIONES DE AUTORIDAD ---

    public void TeletransportarASpawn(Vector3 nuevaPosicion, Quaternion nuevaRotacion)
    {
        if (IsServer)
        {
            NetworkTransform netTransform = GetComponent<NetworkTransform>();
            
            // Si el NetworkTransform es autoritativo del Servidor, usamos el método oficial de Netcode
            if (netTransform != null && netTransform.CanCommitToTransform)
            {
                netTransform.Teleport(nuevaPosicion, nuevaRotacion, transform.localScale);
            }
            else
            {
                // Si no, lo movemos de forma clásica en el servidor
                transform.position = nuevaPosicion;
                transform.rotation = nuevaRotacion;
            }

            // Forzamos la actualización inmediata en los clientes para que no reclamen autoridad
            CambiarPosicionClientRpc(nuevaPosicion, nuevaRotacion);
        }
    }

    [ClientRpc]
    private void CambiarPosicionClientRpc(Vector3 pos, Quaternion rot)
    {
        // El Host ya cambió su posición en la función de arriba, evitamos bucles
        if (IsServer) return;

        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        
        if (netTransform != null)
        {
            // Apagamos el sincronizador un milisegundo para poder forzar las coordenadas sin que salte el filtro de red
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