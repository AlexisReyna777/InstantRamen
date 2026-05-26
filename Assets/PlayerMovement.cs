using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    // Subimos la velocidad base a 10f para que se note el incremento inicial
    [SerializeField] private float speed = 20f;

    void Start()
    {
        // Cambia el color solo al jugador que es dueño de esta instancia
        if (IsOwner)
        {
            // Nota de seguridad: Si tu player tiene hijos, asegúrate de que el Renderer esté en la raíz, 
            // si no, usa GetComponentInChildren<Renderer>()
            if (TryGetComponent<Renderer>(out Renderer render))
            {
                render.material.color = Color.red;
            }
        }
    }

    void Update()
    {
        // REGLA DE NETCODE: Solo el dueño del personaje puede moverlo
        if (!IsOwner) return;

        float x = 0f;
        float z = 0f;

        // Control con WASD
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;

        // Control con Flechas
        if (Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.UpArrow))    z += 1f;
        if (Input.GetKey(KeyCode.DownArrow))  z -= 1f;

        // Creamos el vector de movimiento y lo normalizamos para evitar que corra más rápido en diagonales
        Vector3 move = new Vector3(x, 0f, z);

        // Solo aplicamos movimiento si el jugador realmente está presionando alguna tecla
        if (move.magnitude > 0.1f)
        {
            move = move.normalized;
            transform.position += move * speed * Time.deltaTime;
        }
    }
}