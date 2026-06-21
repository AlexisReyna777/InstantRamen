using UnityEngine;

public class ObjetoCaidaMenu : MonoBehaviour
{
    private float supY;
    private float infY;
    private float velocidadCaida;
    private Vector3 velocidadRotacion;
    
    private float xFijaPilar;
    private bool usaPilarFijo = false;

    public void Configurar(float superiorY, float inferiorY, float anchoX, float rangoZ, float velocidad)
    {
        supY = superiorY;
        infY = inferiorY;
        velocidadCaida = velocidad;

        // Modificado: Rotaciones un poco más suaves para simular el vaivén del viento
        velocidadRotacion = new Vector3(Random.Range(15, 45), Random.Range(15, 45), Random.Range(15, 45));
    }

    public void FijarLadoPilar(float xPos)
    {
        xFijaPilar = xPos;
        usaPilarFijo = true;
    }

    private void Update()
    {
        // 1. CAÍDA ABSOLUTA: Forzamos al objeto a ir hacia abajo de forma recta en el mundo entero
        transform.position += Vector3.down * velocidadCaida * Time.deltaTime;

        // 2. ROTACIÓN PURA: Rotamos usando Space.World para que el giro no afecte la dirección de la caída
        transform.Rotate(velocidadRotacion * Time.deltaTime, Space.World);

        // 3. El Bucle (Respawn arriba)
        if (transform.position.y < infY)
        {
            float nuevaX = usaPilarFijo ? xFijaPilar : transform.position.x;

            transform.position = new Vector3(
                nuevaX,
                supY,
                transform.position.z
            );
            
            transform.rotation = Random.rotation;
        }
    }
}