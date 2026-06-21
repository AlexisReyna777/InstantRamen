using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class IndicadorDashUI : MonoBehaviour
{
    private Image imagenIcono;
    private PlayerMovement jugadorLocal;

    [Header("Configuración Visual")]
    [Tooltip("El tipo de comportamiento que quieres en el icono.")]
    [SerializeField] private ModoVisual modo = ModoVisual.Transparencia;

    [Range(0f, 1f)]
    [SerializeField] private float alfaNoDisponible = 0.2f;

    public enum ModoVisual
    {
        Transparencia, // Opaco cuando está listo, transparente en cooldown
        RelojFill      // Efecto radial de recarga (requiere Image Type = Filled)
    }

    void Awake()
    {
        imagenIcono = GetComponent<Image>();
    }

    void Update()
    {
        // Si aún no tenemos asignado el jugador local, intentamos buscar el que nos pertenece
        if (jugadorLocal == null)
        {
            BuscarJugadorLocal();
            return; 
        }

        // Obtener el estado del cooldown (va de 0 a 1)
        float cooldownProgreso = jugadorLocal.PorcentajeCooldownDash;

        if (modo == ModoVisual.Transparencia)
        {
            // Restablece el tipo de imagen a simple por seguridad
            imagenIcono.type = Image.Type.Simple;
            
            Color colorActual = imagenIcono.color;
            colorActual.a = (cooldownProgreso >= 1f) ? 1f : alfaNoDisponible;
            imagenIcono.color = colorActual;
        }
        else if (modo == ModoVisual.RelojFill)
        {
            // Forzamos a que actúe como un indicador radial de carga
            imagenIcono.type = Image.Type.Filled;
            imagenIcono.fillMethod = Image.FillMethod.Radial360;
            imagenIcono.fillOrigin = (int)Image.Origin360.Top;
            
            imagenIcono.fillAmount = cooldownProgreso;
            
            // Mantenemos opacidad al máximo para que se note la porción rellena
            Color colorActual = imagenIcono.color;
            colorActual.a = 1f;
            imagenIcono.color = colorActual;
        }
    }

    private void BuscarJugadorLocal()
    {
        // Buscamos todos los personajes en el juego
        PlayerMovement[] jugadores = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var jugador in jugadores)
        {
            // Vinculamos únicamente el que controlo yo de manera local (IsOwner)
            if (jugador.IsOwner)
            {
                jugadorLocal = jugador;
                break;
            }
        }
    }
}