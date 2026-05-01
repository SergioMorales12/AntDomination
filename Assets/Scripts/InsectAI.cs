using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public enum InsectState
{
    Idle,
    Walking,
    Interacting,
    Attacking,
    Carrying,
    Grabbed,
    Dead
}

[RequireComponent(typeof(NavMeshAgent))]
public class InsectAI : MonoBehaviour
{
    [Header("Datos")]
    public InsectDataSO data;
    public AntDataSO antData = null;

    [Header("Estado")]
    public InsectState currentState = InsectState.Idle;

    NavMeshAgent agent;

    [Header("Animator")]
    public Animator anim;
    public GameObject mouthPoint;

    float stateTimer;
    Transform target;
    GameObject carriedItem;

    Transform NestTransform => Nest.Instance != null ? Nest.Instance.transform : null;
    // true si es enemigo (no tiene antData)
    public bool isEnemy ;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (anim == null)
            anim = GetComponent<Animator>();

        if (data != null)
            agent.speed = data.speed;
    }

    void Start() => currentHp = data != null ? data.hp : 100f;

    void Update()
    {
        stateTimer += Time.deltaTime;
        switch (currentState)
        {
            case InsectState.Idle:        UpdateIdle();        break;
            case InsectState.Walking:     UpdateWalking();     break;
            case InsectState.Interacting: UpdateInteracting(); break;
            case InsectState.Attacking:   UpdateAttacking();   break;
            case InsectState.Carrying:    UpdateCarrying();    break;
            case InsectState.Grabbed:     UpdateGrabbed();     break;
            case InsectState.Dead:        UpdateDead();        break;
        }
    }

    // ───────────────────────────────────────────
    // ESTADOS
    // ───────────────────────────────────────────

    void UpdateIdle()
    {
        if (ScanForEnemy(out Transform enemy))
        {
            Debug.Log("Enemigo detectado, atacando");
            target = enemy;
            agent.SetDestination(target.position);
            ChangeState(InsectState.Attacking);
            return;
        }
        if (!isEnemy && antData.forageWeight > 0.5f)
        {
            GameObject food = FindNearbyFood(data.aggroRange);
            if (food != null)
            {
                target = food.transform;
                agent.SetDestination(target.position);
                ChangeState(InsectState.Walking);
                return;
            }
        }

        if (stateTimer > data.idleWaitTime)
        {
            agent.SetDestination(GetRandomNavPoint(data.aggroRange));
            ChangeState(InsectState.Walking);
        }
    }

    void UpdateWalking()
    {
        /*if (ScanForEnemy(out Transform enemy))
        {
            target = enemy;
            //agent.SetDestination(target.position);
            ChangeState(InsectState.Attacking);
            return;
        }*/

        // Si ya vamos hacia comida, comprobar si llegamos
        if (target != null && target.CompareTag("Food"))
        {
            if (Vector3.Distance(transform.position, target.position) < 0.5f)
            {
                carriedItem = target.gameObject;
                carriedItem.transform.SetParent(transform);
                carriedItem.transform.position = mouthPoint.transform.position;
                target = null;
                ChangeState(InsectState.Carrying);
            }
            return;
        }

        // Llegó al destino de patrulla
        if (!agent.pathPending && agent.remainingDistance < 0.3f)
        {
            ChangeState(InsectState.Idle);
        }
    }

    void UpdateInteracting()
    {
        if (stateTimer > data.interactTime)
            ChangeState(InsectState.Idle);
    }

    void UpdateAttacking()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Debug.Log("Target lost");
            ChangeState(InsectState.Idle);
            return;
        }

        // Solo las hormigas aliadas huyen
        if (!isEnemy && GetHpPercent() < antData.hpFleeThreshold)
        {
            Debug.Log("Huyendo por poca vida");
            agent.isStopped = false;
            agent.SetDestination(NestTransform.position);
            ChangeState(InsectState.Walking);
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > data.attackRange)
        {
            //Debug.Log($"{gameObject.name} persiguiendo a {target.name}. Distancia: {dist}");
            agent.isStopped = false;
            agent.SetDestination(target.position);
            
        }
        else
        {
            //Debug.Log($"{gameObject.name} ATACANDO a {target.name}! (Plantado en el sitio)");
            if (!agent.isStopped) 
            {
                agent.ResetPath();
                agent.isStopped = true; // Nos plantamos firmes para atacar
            }

            if (stateTimer > data.attackCooldown)
            {
                stateTimer = 0f;
                DealDamage(target);
                // anim?.SetTrigger("Attack"); // ¡Descomenta esto cuando pongas la animación!
            }
        }
    }

    void UpdateCarrying()
    {
        if (NestTransform == null || carriedItem == null)
        {
            ChangeState(InsectState.Idle);
            return;
        }

        agent.SetDestination(NestTransform.position);

        if (Vector3.Distance(transform.position, NestTransform.position) < 1f)
        {
            Nest.Instance.AddFood(1); // Añadimos comida al nido
            Destroy(carriedItem);
            carriedItem = null;
            ChangeState(InsectState.Idle);
        }
    }

    void UpdateGrabbed() { }

    void UpdateDead()
    {
        if (stateTimer > 2f)
        {
            if (isEnemy)
            {
                Nest.Instance.AddGold(data.goldReward); // Recompensa por matar enemigo
                Nest.Instance.GainXP(data.xpReward);  // XP por matar enemigo
            }
            Nest.Instance.UnregisterAnt(this);
            Destroy(gameObject);
        }
    }

    // ───────────────────────────────────────────
    // CAMBIO DE ESTADO
    // ───────────────────────────────────────────

    public void ChangeState(InsectState newState)
    {
        if (currentState == newState) return;

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
        stateTimer   = 0f;

        switch (currentState)
        {
            case InsectState.Idle:
                agent.isStopped = true;
                anim?.SetTrigger("Idle");
                break;
            case InsectState.Walking:
                agent.isStopped = false;
                anim?.SetTrigger("Walking");
                break;
            case InsectState.Attacking:
                agent.isStopped = false;
                //agent.ResetPath();
                anim?.SetTrigger("Interact");
                break;
            case InsectState.Carrying:
                agent.isStopped = false;
                anim?.SetTrigger("Interact");
                break;
            case InsectState.Grabbed:
                agent.enabled = false;
                GetComponent<Rigidbody>().isKinematic = true;
                //anim?.SetTrigger("Struggle");
                break;
            case InsectState.Dead:
                agent.enabled = false;
                //anim?.SetTrigger("Die");
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

    bool ScanForEnemy(out Transform enemy)
    {
        enemy = null;
        float closest = Mathf.Infinity;

        if (data == null)
        {
            Debug.LogWarning($"¡Cuidado! A {gameObject.name} le falta asignar su InsectDataSO en el Inspector.");
            return false;
        }

        if (!isEnemy)
        {
            if (antData == null)
            {
                Debug.LogWarning($"¡Cuidado! A la hormiga aliada {gameObject.name} le falta asignar su AntDataSO en el Inspector.");
                return false;
            }

            // Obreras con fightWeight bajo no atacan solas
            if (antData.fightWeight < 0.3f) return false;
        }
        
        Collider[] hits = Physics.OverlapSphere(transform.position, data.aggroRange);
        foreach (var hit in hits)
        {
            // 1. Buscamos el script InsectAI en el objeto golpeado O en su padre
            var otherAI = hit.GetComponentInParent<InsectAI>();

            // 2. Si es una pared/suelo, o si es ÉL MISMO atacando a sus propios hijos, lo ignoramos
            if (otherAI == null || otherAI == this) continue;

            // 3. A partir de aquí, usamos otherAI.gameObject para mirar los Tags (el padre real)
            bool targetIsEnemy = otherAI.CompareTag("Enemy");
            bool targetIsAnt   = otherAI.CompareTag("Ant"); // Veo que añadiste esto en tu último código

            // 4. Lógica de bandos
            if (isEnemy && targetIsEnemy && !targetIsAnt) continue;    
            if (!isEnemy && !targetIsEnemy) continue;  

            if (otherAI.currentState == InsectState.Dead) continue;
            
            // 5. Calculamos la distancia contra el padre real, no contra el hijo
            float d = Vector3.Distance(transform.position, otherAI.transform.position);
            if (d < closest) 
            { 
                closest = d; 
                enemy = otherAI.transform; // Guardamos al padre real como objetivo
            }
        }
        return enemy != null;
    }

    void DealDamage(Transform t)
    {
        var other = t.GetComponent<InsectAI>();
        if (other == null || other.currentState == InsectState.Dead) return;
        other.TakeDamage(data.damage);
    }

    public void TakeDamage(float amount)
    {
        currentHp -= amount;
        if (!isEnemy && GetHpPercent() < antData.hpFleeThreshold)
        {
            Debug.Log("Huyendo por poca vida");
            agent.isStopped = false;
            agent.SetDestination(NestTransform.position);
            ChangeState(InsectState.Walking);
            return;
        }
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

    float currentHp;
}