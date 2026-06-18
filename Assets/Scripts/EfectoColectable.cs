using UnityEngine;

public class EfectoColectable : MonoBehaviour
{
    [Header("Configuración de Giro")]
    [SerializeField] private float velocidadGiro = 50f;

    [Header("Configuración de Flote")]
    [SerializeField] private float velocidadFlotar = 2f; // Qué tan rápido sube y baja
    [SerializeField] private float amplitudFlotar = 0.15f; // Qué tanto se desplaza (altura)

    private Vector3 posicionInicial;

    void Start()
    {
        // Guardamos la posición exacta en la que el objeto fue spawneado
        posicionInicial = transform.position;
    }

    void Update()
    {
        // 1. Girar continuamente sobre su propio eje Y
        transform.Rotate(Vector3.up, velocidadGiro * Time.deltaTime);

        // 2. Desplazamiento arriba/abajo usando una onda Seno (Mathf.Sin)
        // Esto crea un movimiento suave y natural, como si flotara en el agua
        float nuevoY = posicionInicial.y + Mathf.Sin(Time.time * velocidadFlotar) * amplitudFlotar;
        
        // Aplicamos la nueva posición manteniendo las coordenadas X y Z originales
        transform.position = new Vector3(transform.position.x, nuevoY, transform.position.z);
    }

    public void DesactivarEfectoYRotar(Quaternion nuevaRotacion)
    {
        this.enabled = false;

        transform.localRotation = nuevaRotacion;
    }
}
