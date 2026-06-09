using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float speed = 7f;
    
    private Animator miAnimator;

    public override void OnNetworkSpawn()
    {
        // Buscamos el Animator en los objetos hijos (tu modelo de Blender)
        miAnimator = GetComponentInChildren<Animator>();

        // Si somos el dueño de este personaje, cambiamos el color de su malla visual
        if (IsOwner)
        {
            Renderer rendererHijo = GetComponentInChildren<Renderer>();
            if (rendererHijo != null)
            {
                rendererHijo.material.color = Color.red;
            }
            else
            {
                Debug.LogWarning("No se encontró un Renderer en los hijos del Player para cambiar el color.");
            }
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (GameManager.Instance != null && !GameManager.Instance.partidaIniciada.Value) return;

        // 1. Leemos los ejes de entrada nativos de Unity (WASD / Flechas)
        float temporalX = Input.GetAxisRaw("Horizontal");
        float temporalZ = Input.GetAxisRaw("Vertical");

        float inputX = 0f;
        float inputZ = 0f;

        // 2. FILTRO DE 4 DIRECCIONES: Forzamos el movimiento en cruz (elimina las diagonales)
        // Comparamos qué eje tiene más presión en el teclado usando valores absolutos.
        if (Mathf.Abs(temporalX) >= Mathf.Abs(temporalZ))
        {
            // Si domina el movimiento horizontal (o son iguales), anulamos el avance vertical
            inputX = temporalX;
            inputZ = 0f;
        }
        else
        {
            // Si domina el movimiento vertical, anulamos el avance horizontal
            inputX = 0f;
            inputZ = temporalZ;
        }

        // 3. Creamos el vector de movimiento con los ejes ya filtrados
        Vector3 move = new Vector3(inputX, 0f, inputZ);

        // 4. Mandamos la velocidad limpia al Animator (0 si está quieto, 1 si se mueve)
        float velocidadActual = move.magnitude;
        if (miAnimator != null)
        {
            miAnimator.SetFloat("Velocidad", velocidadActual);
        }  

        // 5. Si hay movimiento, aplicamos la rotación exacta y el desplazamiento físico
        if (move.magnitude > 0.1f)
        {
            // Como el filtro ya anuló un eje, el vector ya mide 1 de forma perfecta (no hace falta normalized)
            
            // Calculamos la rotación fija (0°, 90°, 180° o 270°)
            Quaternion rotacionObjetivo = Quaternion.LookRotation(move);

            // Aplicamos el cambio de frente inmediato sin bucles de indecisión
            if (Quaternion.Angle(transform.rotation, rotacionObjetivo) > 0.1f)
            {
                transform.rotation = rotacionObjetivo;
            }

            // Desplazamiento lineal en el mapa
            transform.position += move * speed * Time.deltaTime;
        }
    }
}