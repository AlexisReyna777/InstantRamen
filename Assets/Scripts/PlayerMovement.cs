using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;
using System.Collections.Generic;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float speed = 5f; 
    
    [Header("Mecánica de Peso y Apilamiento")]
    [SerializeField] private Transform puntoAnclajeCajas; 
    [SerializeField] private Vector3 dimensionesCaja = new Vector3(0f, 0.5f, 0f); 
    [SerializeField] private Vector3 rotacionCajaApilada = new Vector3(0f, 90f, 0f);
    [SerializeField] private float penalizacionPorObjeto = 0.5f; 
    [SerializeField] private float velocidadMinima = 1.5f; 

    [Header("Mecánica de Tacleo / Dash")]
    [SerializeField] private float fuerzaDash = 25f;
    [SerializeField] private float duracionDash = 0.25f;
    [SerializeField] private float cooldownDash = 1.5f;
    [SerializeField] private float duracionStun = 1.2f;
    [SerializeField] private float fuerzaEmpujonRecibido = 10f;
    [Tooltip("Multiplicador de fuerza cuando el jugador con MENOS puntos taclea al líder de la partida")]
    [SerializeField] private float multiplicadorEmpujonDesventaja = 0.35f; 

    [Header("Modelos Visuales")]
    [SerializeField] private GameObject modeloHost;
    [SerializeField] private GameObject modeloCliente;

    [Header("Efectos de Audio")]
    [SerializeField] private AudioSource audioSourceComponente; 
    [SerializeField] private AudioClip sonidoRecogerCaja; 

    [Header("Avatars Específicos")]
    [SerializeField] private Avatar avatarHost;
    [SerializeField] private Avatar actorCliente; 
    [SerializeField] private Avatar avatarCliente;
    
    private bool controlesInvertidos = false;
    private Coroutine rutinaInversion;
    private Animator miAnimator;
    private bool esDiminuto = false;
    private Coroutine rutinaTamano;
    [SerializeField] private float multiplicadorVelocidadChiquito = 1.35f;

    private bool estaDasheando = false;
    private bool estaAturdido = false;
    private float tiempoSiguienteDash = 0f;

    public int colectablesActuales { get; private set; } = 0;
    private float velocidadModificada;
    private List<GameObject> cajasCargadasVisuales = new List<GameObject>();

    private Rigidbody miRigidbody;
    
    // NUEVO: Vector de control de dirección puente entre Update y FixedUpdate
    private Vector3 direccionMovimiento = Vector3.zero;

    public float PorcentajeCooldownDash
    {
        get
        {
            if (Time.time >= tiempoSiguienteDash) return 1f;
            float tiempoRestante = tiempoSiguienteDash - Time.time;
            return 1f - (tiempoRestante / cooldownDash);
        }
    }

    public List<GameObject> ObtenerCajasCargadas() 
    { 
        return cajasCargadasVisuales; 
    }

    private void Awake()
    {
        miAnimator = GetComponent<Animator>();
        miRigidbody = GetComponent<Rigidbody>(); 
        velocidadModificada = speed; 
    }

    public override void OnNetworkSpawn()
    {
        ActualizarVisualesYAvatar();
        StartCoroutine(RutinaSeguridadSpawnFisico());

    }

    private IEnumerator RutinaSeguridadSpawnFisico()
    {
        if (miRigidbody != null)
        {
            miRigidbody.detectCollisions = false;
            miRigidbody.isKinematic = true;
            miRigidbody.linearVelocity = Vector3.zero;
            miRigidbody.angularVelocity = Vector3.zero;
        }

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (miRigidbody != null)
        {
            miRigidbody.linearVelocity = Vector3.zero;
            miRigidbody.angularVelocity = Vector3.zero;
            miRigidbody.isKinematic = false;
            miRigidbody.detectCollisions = true;
        }
    }

    private void ActualizarVisualesYAvatar()
    {
        if (miAnimator == null) miAnimator = GetComponent<Animator>();

        if (OwnerClientId == 0) 
        {
            if (modeloHost != null) modeloHost.SetActive(true);
            if (modeloCliente != null) modeloCliente.SetActive(false);
            if (miAnimator != null && avatarHost != null) miAnimator.avatar = avatarHost;
        }
        else 
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

        // Si está bajo control físico externo, cancelamos la recolección de comandos
        if (estaAturdido || estaDasheando) 
        {
            direccionMovimiento = Vector3.zero;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= tiempoSiguienteDash)
        {
            StartCoroutine(RutinaDashLocal());
            tiempoSiguienteDash = Time.time + cooldownDash;
        }

        float temporalX = Input.GetAxisRaw("Horizontal");
        float temporalZ = Input.GetAxisRaw("Vertical");

        float inputX = 0f;
        float inputZ = 0f;

        if (controlesInvertidos)
        {
            temporalX *= -1f;
            temporalZ *= -1f;
        }

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

        // NUEVO: Guardamos la dirección del movimiento calculada para procesarla físicamente
        direccionMovimiento = new Vector3(inputX, 0f, inputZ);
        float velocidadActual = direccionMovimiento.magnitude;

        if (miAnimator != null)
        {
            miAnimator.SetFloat("Velocidad", velocidadActual);
        }
        
        SincronizarAnimacionServerRpc(velocidadActual);

        if (direccionMovimiento.magnitude > 0.1f)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccionMovimiento);
            if (Quaternion.Angle(transform.rotation, rotacionObjetivo) > 0.1f)
            {
                transform.rotation = rotacionObjetivo;
            }
        }
    }

    // NUEVO: Bloque de actualización física fija nativa
    void FixedUpdate()
    {
        if (!IsOwner) return;

        // Si está bajo efectos de fuerzas externas como el dash o aturdimiento, dejamos que se procesen de forma pura
        if (estaAturdido || estaDasheando) return;

        if (miRigidbody != null)
        {
            // Calculamos el desplazamiento basándonos en la dirección y su velocidad asignada, respetando la caída/gravedad en Y
            Vector3 velocidadObjetivo = direccionMovimiento * velocidadModificada;
            miRigidbody.linearVelocity = new Vector3(velocidadObjetivo.x, miRigidbody.linearVelocity.y, velocidadObjetivo.z);
        }
    }

    private void LateUpdate()
    {
        if (puntoAnclajeCajas == null || cajasCargadasVisuales.Count == 0) return;

        for (int i = 0; i < cajasCargadasVisuales.Count; i++)
        {
            if (cajasCargadasVisuales[i] == null) continue;
            Vector3 desfaceAltura = dimensionesCaja * i;
            cajasCargadasVisuales[i].transform.position = puntoAnclajeCajas.position + (puntoAnclajeCajas.rotation * desfaceAltura);
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
        if (miAnimator != null) miAnimator.SetFloat("Velocidad", velocidad);
    }

    private IEnumerator RutinaDashLocal()
    {
        estaDasheando = true;

        if (miRigidbody != null)
        {
            // NUEVO: Movimiento de Dash físico real manteniendo la Y de gravedad
            Vector3 direccionDash = transform.forward * fuerzaDash;
            miRigidbody.linearVelocity = new Vector3(direccionDash.x, miRigidbody.linearVelocity.y, direccionDash.z);
        }

        yield return new WaitForSeconds(duracionDash);

        if (miRigidbody != null)
        {
            miRigidbody.linearVelocity = new Vector3(0f, miRigidbody.linearVelocity.y, 0f);
        }

        estaDasheando = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOwner || !estaDasheando) return;

        if (collision.gameObject.TryGetComponent<PlayerMovement>(out var rival))
        {
            SolicitarTacleoServerRpc(rival.NetworkObjectId, transform.forward);
        }
    }

    [ServerRpc]
    private void SolicitarTacleoServerRpc(ulong rivalNetworkObjectId, Vector3 direccionAtaque)
    {
        if (GameManager.Instance == null) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(rivalNetworkObjectId, out var netObj))
        {
            if (netObj.TryGetComponent<PlayerMovement>(out var scriptRival))
            {
                int puntosAtacante = (this.OwnerClientId == 0) ? GameManager.Instance.puntajeHost.Value : GameManager.Instance.puntajeCliente.Value;
                int puntosRival = (scriptRival.OwnerClientId == 0) ? GameManager.Instance.puntajeHost.Value : GameManager.Instance.puntajeCliente.Value;

                Vector3 direccionEmpujon = direccionAtaque;
                direccionEmpujon.y = 0;

                float modificadorFuerzaFinal = 1.0f;
                if (puntosAtacante <= puntosRival)
                {
                    modificadorFuerzaFinal = multiplicadorEmpujonDesventaja;
                }

                scriptRival.RecibirTacleoClientRpc(direccionEmpujon.normalized, modificadorFuerzaFinal);
            }
        }
    }

    [ClientRpc]
    public void RecibirTacleoClientRpc(Vector3 direccionEmpujon, float multiplicadorFuerza)
    {
        if (estaAturdido) return;
        StartCoroutine(RutinaAturdimiento(direccionEmpujon, multiplicadorFuerza));
    }

    private IEnumerator RutinaAturdimiento(Vector3 direccionEmpujon, float multiplicadorFuerza)
    {
        estaAturdido = true;
        if (miAnimator != null) miAnimator.SetFloat("Velocidad", 0f);

        float fuerzaCalculada = fuerzaEmpujonRecibido * multiplicadorFuerza;

        if (miRigidbody != null)
        {
            // NUEVO: Reseteamos velocidad y aplicamos impulso físico real
            miRigidbody.linearVelocity = Vector3.zero; 
            miRigidbody.AddForce(direccionEmpujon * fuerzaCalculada, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(duracionStun);

        if (miRigidbody != null)
        {
            miRigidbody.linearVelocity = new Vector3(0f, miRigidbody.linearVelocity.y, 0f);
        }

        estaAturdido = false;
    }

    public void RecogerColectable(GameObject objetoCaja)
    {
        if (objetoCaja.TryGetComponent<EfectoColectable>(out var efecto))
        {
            efecto.DesactivarEfectoYRotar(Quaternion.Euler(rotacionCajaApilada));
        }

        if (!cajasCargadasVisuales.Contains(objetoCaja))
        {
            cajasCargadasVisuales.Add(objetoCaja);
        }

        colectablesActuales++;
        RecalcularVelocidad();

        if (IsOwner && audioSourceComponente != null && sonidoRecogerCaja != null)
        {
            audioSourceComponente.PlayOneShot(sonidoRecogerCaja);
        }
    }

    public void EntregarColectablesDesdeServidor()
    {
        if (!IsServer) return;
        LimpiarPesoClientRpc();
    }

    [ClientRpc]
    private void LimpiarPesoClientRpc()
    {
        if (rutinaTamano != null)
        {
            StopCoroutine(rutinaTamano);
            rutinaTamano = null;
        }

        esDiminuto = false;
        cajasCargadasVisuales.Clear();
        colectablesActuales = 0; 
        
        if (GameManager.Instance != null)
        {
            int puntosDeEsteJugador = (OwnerClientId == 0) ? GameManager.Instance.puntajeHost.Value : GameManager.Instance.puntajeCliente.Value;
            ActualizarEscalaPorPuntaje(puntosDeEsteJugador);
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        RecalcularVelocidad();
    }

    private void RecalcularVelocidad()
    {
        float calculoNuevaVelocidad = speed - (colectablesActuales * penalizacionPorObjeto);
        velocidadModificada = Mathf.Max(calculoNuevaVelocidad, velocidadMinima);

        if (esDiminuto)
        {
            velocidadModificada *= multiplicadorVelocidadChiquito;
        }
    }

    public void TeletransportarASpawn(Vector3 nuevaPosicion, Quaternion nuevaRotacion)
    {
        if (IsServer)
        {
            LimpiarPesoClientRpc();
            NetworkTransform netTransform = GetComponent<NetworkTransform>();
            
            if (miRigidbody != null)
            {
                miRigidbody.linearVelocity = Vector3.zero;
                miRigidbody.angularVelocity = Vector3.zero;
            }

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
        
        if (miRigidbody != null)
        {
            miRigidbody.linearVelocity = Vector3.zero;
            miRigidbody.angularVelocity = Vector3.zero;
        }

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
    }

    public void AplicarEfectoInversion(float duracion)
    {
        if (rutinaInversion != null) StopCoroutine(rutinaInversion);
        rutinaInversion = StartCoroutine(RutinaInvertirControles(duracion));
    }

    private IEnumerator RutinaInvertirControles(float duracion)
    {
        if (IsOwner) controlesInvertidos = true;
        yield return new WaitForSeconds(duracion);
        controlesInvertidos = false;
        rutinaInversion = null;
    }

    public void AplicarEfectoEncoger(float duracion)
    {
        if (rutinaTamano != null) StopCoroutine(rutinaTamano);
        rutinaTamano = StartCoroutine(RutinaEncogerPersonaje(duracion));
    }

    private IEnumerator RutinaEncogerPersonaje(float duracion)
    {
        esDiminuto = true;
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        RecalcularVelocidad();

        yield return new WaitForSeconds(duracion);

        esDiminuto = false;
        if (GameManager.Instance != null)
        {
            int puntosDeEsteJugador = (OwnerClientId == 0) ? GameManager.Instance.puntajeHost.Value : GameManager.Instance.puntajeCliente.Value;
            ActualizarEscalaPorPuntaje(puntosDeEsteJugador);
        }
        else
        {
            transform.localScale = Vector3.one;
        }
        
        RecalcularVelocidad();
        rutinaTamano = null;
    }

    public void ActualizarEscalaPorPuntaje(int puntosActuales)
    {
        float nuevoFactor = 1.0f + (puntosActuales * 0.04f); 
        nuevoFactor = Mathf.Clamp(nuevoFactor, 1.0f, 2.5f); 

        if (!esDiminuto)
        {
            transform.localScale = Vector3.one * nuevoFactor;
        }
    }
}