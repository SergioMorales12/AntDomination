using UnityEngine;
using UnityEngine.AI;
public enum InsectState
{
    Idle,           // Inactivo / Esperando
    Walking,        // Andando / Patrullando
    Interacting,    // Interactuando (comiendo, construyendo)
    Attacking,      // Atacando a otro insecto
    Carrying,       // Cargando comida / Recursos
    Grabbed,        // Siendo agarrado por el jugador (Realidad Mixta)
    Dead            // Muerto
}
[RequireComponent(typeof(NavMeshAgent))]
public class InsectAI : MonoBehaviour
{
    [Header("Datos")]
    public InsectDataSO data;         // arrastra el SO en el Inspector
    public AntDataSO antData;         // solo si es hormiga tuya; null si es enemigo

    [Header("Estado")]
    public InsectState currentState = InsectState.Idle;

    // Referencias internas
    NavMeshAgent agent;
    Animator anim;
    float stateTimer;
    Transform target;        // enemigo u objetivo actual
    GameObject carriedItem;  // recurso que lleva encima

    // Cache del nido (se busca una sola vez)
    static Transform nestTransform;
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim  = GetComponent<Animator>();

        if (nestTransform == null)
            nestTransform = GameObject.FindGameObjectWithTag("Nest")?.transform;

        // Aplicar stats del SO al agente
        if (data != null)
        {
            agent.speed = data.speed;
        }
    }
    
    void Update()
    {
        stateTimer += Time.deltaTime;
        // La máquina de estados principal
        switch (currentState)
        {
            case InsectState.Idle:
                UpdateIdle();
                break;
            case InsectState.Walking:
                UpdateWalking();
                break;
            case InsectState.Interacting:
                UpdateInteracting();
                break;
            case InsectState.Attacking:
                UpdateAttacking();
                break;
            case InsectState.Carrying:
                UpdateCarrying();
                break;
            case InsectState.Grabbed:
                UpdateGrabbed();
                break;
            case InsectState.Dead:
                UpdateDead();
                break;
        }
    }

     // ───────────────────────────────────────────
    // ESTADOS
    // ───────────────────────────────────────────

    void UpdateIdle()
    {
        // Espera un momento y luego patrulla, pero primero comprueba amenazas
        if (ScanForEnemy(out Transform enemy))
        {
            target = enemy;
            ChangeState(InsectState.Attacking);
            return;
        }

        if (stateTimer > data.idleWaitTime)
        {
            Vector3 randomPoint = GetRandomNavPoint(8f);
            agent.SetDestination(randomPoint);
            ChangeState(InsectState.Walking);
        }
    }

    void UpdateWalking()
    {
        // Sigue comprobando enemigos mientras camina
        if (ScanForEnemy(out Transform enemy))
        {
            target = enemy;
            agent.ResetPath();
            ChangeState(InsectState.Attacking);
            return;
        }

        // Llegó al destino
        if (!agent.pathPending && agent.remainingDistance < 0.3f)
        {
            // Si es hormiga con alto forageWeight, busca comida al llegar
            if (antData != null && antData.forageWeight > 0.5f)
            {
                GameObject food = FindNearbyFood(5f);
                if (food != null)
                {
                    target = food.transform;
                    agent.SetDestination(target.position);
                    // Al llegar recogemos en UpdateWalking el siguiente frame;
                    // usamos distancia para saber que llegamos al recurso
                    if (Vector3.Distance(transform.position, target.position) < 0.5f)
                    {
                        carriedItem = food;
                        food.transform.SetParent(transform);
                        food.transform.localPosition = Vector3.up * 0.3f;
                        ChangeState(InsectState.Carrying);
                        return;
                    }
                }
            }

            ChangeState(InsectState.Idle);
        }
    }

    void UpdateInteracting()
    {
        // Animación de interacción; dura interactTime segundos y vuelve a Idle
        if (stateTimer > data.interactTime)
            ChangeState(InsectState.Idle);
    }

    void UpdateAttacking()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            ChangeState(InsectState.Idle);
            return;
        }

        // Huir si HP baja (solo hormigas con hpFleeThreshold configurado)
        if (antData != null && GetHpPercent() < antData.hpFleeThreshold)
        {
            agent.SetDestination(nestTransform.position);
            ChangeState(InsectState.Walking);
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);

        if (dist > data.attackRange)
        {
            // Todavía lejos: acercarse
            agent.SetDestination(target.position);
        }
        else
        {
            // En rango: golpear en cada ciclo de ataque
            agent.ResetPath();
            if (stateTimer > data.attackCooldown)
            {
                stateTimer = 0f;
                DealDamage(target);
            }
        }
    }

    void UpdateCarrying()
    {
        if (nestTransform == null || carriedItem == null)
        {
            ChangeState(InsectState.Idle);
            return;
        }

        agent.SetDestination(nestTransform.position);

        if (Vector3.Distance(transform.position, nestTransform.position) < 1f)
        {
            // Depositar recurso en el nido
            Destroy(carriedItem);
            carriedItem = null;
            // Aquí podrías llamar a NestManager.AddFood(data.carryAmount)
            ChangeState(InsectState.Idle);
        }
    }

    void UpdateGrabbed()
    {
        // El jugador MR controla la posición. Solo reproducimos animación de pataleo.
        // Nada más: el XR Interaction Toolkit mueve el GameObject.
    }

    void UpdateDead()
    {
        // Se ejecuta una sola vez gracias al guard en ChangeState
        // La animación de muerte ya se lanzó en OnEnter; aquí solo esperamos
        // a que termine para destruir el objeto (o pooling).
        if (stateTimer > 2f)
        {
            // Si es enemigo, aplicar recompensas a la colonia
            if (data != null && antData == null)
                //RewardSystem.Apply(data.rewardTags);

            Destroy(gameObject);
        }
    }

    // ───────────────────────────────────────────
    // CAMBIO DE ESTADO — lógica de entrada/salida
    // ───────────────────────────────────────────

    public void ChangeState(InsectState newState)
    {
        if (currentState == newState) return;

        // ── Salida del estado actual ──
        switch (currentState)
        {
            case InsectState.Walking:
                agent.ResetPath();
                agent.isStopped = true;
                break;
            case InsectState.Grabbed:
                agent.enabled = true;
                GetComponent<Rigidbody>().isKinematic = false;
                break;
        }

        currentState = newState;
        stateTimer   = 0f;  // reset del timer en cada transición

        // ── Entrada al nuevo estado ──
        switch (currentState)
        {
            case InsectState.Idle:
                agent.isStopped = true;
                anim?.SetTrigger("Idle");
                break;
            case InsectState.Walking:
                agent.isStopped = false;
                anim?.SetTrigger("Walk");
                break;
            case InsectState.Attacking:
                agent.isStopped = false;
                anim?.SetTrigger("Attack");
                break;
            case InsectState.Carrying:
                agent.isStopped = false;
                anim?.SetTrigger("Carry");
                break;
            case InsectState.Grabbed:
                agent.enabled = false;           // NavMesh no lucha contra la mano MR
                GetComponent<Rigidbody>().isKinematic = true;
                anim?.SetTrigger("Struggle");
                break;
            case InsectState.Dead:
                agent.enabled = false;
                anim?.SetTrigger("Die");
                // Si llevaba algo, suéltalo
                if (carriedItem != null)
                {
                    carriedItem.transform.SetParent(null);
                    carriedItem = null;
                }
                break;
        }
    }

    // ───────────────────────────────────────────
    // HELPERS
    // ───────────────────────────────────────────

    // Detecta el enemigo más cercano dentro del aggroRange del SO
    bool ScanForEnemy(out Transform enemy)
    {
        enemy = null;

        // Las obreras con fightWeight bajo no atacan solas
        if (antData != null && antData.fightWeight < 0.3f) return false;

        Collider[] hits = Physics.OverlapSphere(transform.position, data.aggroRange);
        float closest = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;
            if (!hit.CompareTag("Enemy") && !hit.CompareTag("Insect")) continue;

            float d = Vector3.Distance(transform.position, hit.transform.position);
            if (d < closest) { closest = d; enemy = hit.transform; }
        }

        return enemy != null;
    }

    void DealDamage(Transform t)
    {
        var other = t.GetComponent<InsectAI>();
        if (other == null) return;
        other.TakeDamage(data.damage);
    }

    // Llámalo desde fuera cuando esta hormiga recibe daño
    public void TakeDamage(float amount)
    {
        currentHp -= amount;
        if (currentHp <= 0f && currentState != InsectState.Dead)
            ChangeState(InsectState.Dead);
    }

    float GetHpPercent() => currentHp / data.hp;

    GameObject FindNearbyFood(float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var h in hits)
            if (h.CompareTag("Food")) return h.gameObject;
        return null;
    }

    Vector3 GetRandomNavPoint(float radius)
    {
        Vector3 rand = transform.position + Random.insideUnitSphere * radius;
        NavMesh.SamplePosition(rand, out NavMeshHit hit, radius, NavMesh.AllAreas);
        return hit.position;
    }

    // HP en runtime (no está en el SO porque cambia)
    float currentHp;
    void Start() => currentHp = data != null ? data.hp : 100f;
}