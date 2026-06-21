using UnityEngine;

public class ObjetoCaidaMenu : MonoBehaviour
{
    private float supY;
    private float infY;
    private float rZ;
    private float velocidadCaida;
    private Vector3 velocidadRotacion;
    
    private float xFijaPilar;
    private bool usaPilarFijo = false;

    public void Configurar(float superiorY, float inferiorY, float anchoX, float rangoZ, float velocidad)
    {
        supY = superiorY;
        infY = inferiorY;
        rZ = rangoZ;
        velocidadCaida = velocidad;

        velocidadRotacion = new Vector3(Random.Range(10, 50), Random.Range(10, 50), Random.Range(10, 50));
    }

    public void FijarLadoPilar(float xPos)
    {
        xFijaPilar = xPos;
        usaPilarFijo = true;
    }

    private void Update()
    {
        // 1. Desplazar hacia abajo
        transform.Translate(Vector3.down * velocidadCaida * Time.deltaTime, Space.World);

        // 2. Rotar
        transform.Rotate(velocidadRotacion * Time.deltaTime);

        // 3. El Bucle corregido para mantener los pilares laterales
        if (transform.position.y < infY)
        {
            // Mantenemos la posición X que le corresponde a su pilar
            float nuevaX = usaPilarFijo ? xFijaPilar : transform.position.x;

            transform.position = new Vector3(
                nuevaX,
                supY,
                transform.position.z // Mantiene su profundidad actual
            );
            
            transform.rotation = Random.rotation;
        }
    }
}