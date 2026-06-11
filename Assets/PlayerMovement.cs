using UnityEngine;
using Unity.Netcode;
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
        // Seteamos nuestra propia animación localmente
        if (miAnimator != null)
        {
            miAnimator.SetFloat("Velocidad", velocidadActual);
        }
        // Le avisamos al resto del mundo que nos estamos moviendo
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

    // [ServerRpc] -> El cliente le pide al servidor ejecutar esto
    [ServerRpc]
    private void SincronizarAnimacionServerRpc(float velocidad)
    {
        // El servidor le ordena a todos los clones replicar la velocidad
        SincronizarAnimacionClientRpc(velocidad);
    }

    // [ClientRpc] -> El servidor le cambia el parámetro a todos los clones en la red
    [ClientRpc]
    private void SincronizarAnimacionClientRpc(float velocidad)
    {
        // Si soy el dueño original, ignoramos el mensaje para que no cause lag visual
        if (IsOwner) return;

        // Forzamos a que el clon en las otras pantallas ejecute la animación
        if (miAnimator != null)
        {
            miAnimator.SetFloat("Velocidad", velocidad);
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