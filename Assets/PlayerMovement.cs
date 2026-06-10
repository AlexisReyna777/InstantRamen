using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
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
    [SerializeField] private Avatar avatarCliente;
    
    private Animator miAnimator;

    private void Awake()
    {
        // Capturamos el Animator único de la raíz inmediatamente
        miAnimator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        // Cambiamos mallas, avatares y vinculamos el componente de red con autoridad
        ActualizarVisualesYAvatar();

        // CONFIGURACIÓN DEL COLOR LOCAL
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
        // ¡NUEVO!: Buscamos primero el OwnerNetworkAnimator para la sincronización de cliente a host
        NetworkAnimator networkAnimator = GetComponent<OwnerNetworkAnimator>();
        if (networkAnimator == null) networkAnimator = GetComponent<NetworkAnimator>();
        if (networkAnimator == null) networkAnimator = GetComponentInChildren<NetworkAnimator>();

        if (miAnimator == null) miAnimator = GetComponent<Animator>();

        if (OwnerClientId == 0) // El Host
        {
            if (modeloHost != null) modeloHost.SetActive(true);
            if (modeloCliente != null) modeloCliente.SetActive(false);
            
            // Le ponemos las reglas de huesos del Host al Animator de la raíz
            if (miAnimator != null && avatarHost != null) miAnimator.avatar = avatarHost;
        }
        else // El Cliente
        {
            if (modeloHost != null) modeloHost.SetActive(false);
            if (modeloCliente != null) modeloCliente.SetActive(true);
            
            // Le ponemos las reglas de huesos del Cliente al Animator de la raíz
            if (miAnimator != null && avatarCliente != null) miAnimator.avatar = avatarCliente;
        }

        // Vinculamos el Animator que acabamos de configurar al componente de red
        if (networkAnimator != null && miAnimator != null)
        {
            networkAnimator.Animator = miAnimator;
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
        if (miAnimator != null)
        {
            miAnimator.SetFloat("Velocidad", velocidadActual);
        }  

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

    public void TeletransportarASpawn(Vector3 nuevaPosicion, Quaternion nuevaRotacion)
    {
        if (IsServer)
        {
            transform.position = nuevaPosicion;
            transform.rotation = nuevaRotacion;
            CambiarPosicionClientRpc(nuevaPosicion, nuevaRotacion);
        }
    }

    [ClientRpc]
    private void CambiarPosicionClientRpc(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
        Debug.Log($"[Netcode] Cliente reubicado con éxito en su punto de spawn: {pos}");
    }
}