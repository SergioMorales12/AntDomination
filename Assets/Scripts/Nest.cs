using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// ────────────────────────────────────────────────
//  Tipos de hormiga que el nido puede producir
// ────────────────────────────────────────────────
[System.Serializable]
public class AntRecipe
{
    public string       antName;
    public GameObject   prefab;
    public AntDataSO    antData;
    public int          foodCost;
    public int          goldCost;       // recurso secundario opcional
    public float        productionTime; // segundos que tarda en producirse
}

// ────────────────────────────────────────────────
//  Nest — GameManager del nido
// ────────────────────────────────────────────────
public class Nest : MonoBehaviour
{
    // ── Singleton ──────────────────────────────
    public static Nest Instance { get; private set; }

    // ── Recursos ───────────────────────────────
    [Header("Recursos")]
    [SerializeField] int startFood  = 50;
    [SerializeField] int startGold  = 0;

    public int Food  { get; private set; }
    public int Gold  { get; private set; }

    // ── Nivel ──────────────────────────────────
    [Header("Nivel del nido")]
    [SerializeField] int startLevel         = 1;
    [SerializeField] int xpPerKill          = 10;
    [SerializeField] int xpPerFoodDelivered = 5;
    [SerializeField] int[] xpThresholds = { 0, 100, 250, 500, 1000 }; // xp necesario por nivel

    public int   Level    { get; private set; }
    public int   XP       { get; private set; }
    public int   XPToNext => Level < xpThresholds.Length - 1 ? xpThresholds[Level] : int.MaxValue;

    // ── Hormigas ───────────────────────────────
    [Header("Hormigas")]
    [SerializeField] Transform spawnPoint;
    [SerializeField] List<AntRecipe> recipes = new();

    // Todas las hormigas vivas rastreadas por el nido
    readonly List<InsectAI> activeAnts = new();
    public IReadOnlyList<InsectAI> ActiveAnts => activeAnts;

    // Cola de producción: (receta, tiempo restante)
    readonly Queue<(AntRecipe recipe, float timeLeft)> productionQueue = new();
    float currentProductionTimer = -1f;
    AntRecipe currentProduction  = null;

    // ── Eventos (para la UI) ───────────────────
    [Header("Eventos")]
    public UnityEvent<int> OnFoodChanged;
    public UnityEvent<int> OnGoldChanged;
    public UnityEvent<int> OnLevelUp;
    public UnityEvent      OnAntListChanged;

    // ──────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Food  = startFood;
        Gold  = startGold;
        Level = startLevel;
        XP    = 0;
    }

    void Update()
    {
        TickProduction();
    }

    // ════════════════════════════════════════════
    //  RECURSOS
    // ════════════════════════════════════════════

    /// <summary>Añade comida al nido (llamado por la hormiga al entregar).</summary>
    public void AddFood(int amount)
    {
        Food += amount;
        OnFoodChanged?.Invoke(Food);
        GainXP(xpPerFoodDelivered);
    }

    public bool SpendFood(int amount)
    {
        if (Food < amount) return false;
        Food -= amount;
        OnFoodChanged?.Invoke(Food);
        return true;
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    // ════════════════════════════════════════════
    //  NIVEL / XP
    // ════════════════════════════════════════════

    public void GainXP(int amount)
    {
        XP += amount;
        while (Level < xpThresholds.Length - 1 && XP >= xpThresholds[Level])
        {
            Level++;
            Debug.Log($"[Nest] ¡Nivel {Level}!");
            OnLevelUp?.Invoke(Level);
        }
    }

    /// <summary>Llamar cuando una hormiga aliada mata a un enemigo.</summary>
    public void RegisterKill() => GainXP(xpPerKill);

    // ════════════════════════════════════════════
    //  PRODUCCIÓN DE HORMIGAS
    // ════════════════════════════════════════════

    /// <summary>Encola la producción de una hormiga por su nombre en la lista de recetas.</summary>
    public bool EnqueueAnt(string antName)
    {
        var recipe = recipes.Find(r => r.antName == antName);
        if (recipe == null)
        {
            Debug.LogWarning($"[Nest] Receta '{antName}' no encontrada.");
            return false;
        }
        return EnqueueAnt(recipe);
    }

    public bool EnqueueAnt(AntRecipe recipe)
    {
        if (!SpendFood(recipe.foodCost))
        {
            Debug.Log("[Nest] No hay suficiente comida.");
            return false;
        }
        if (recipe.goldCost > 0 && !SpendGold(recipe.goldCost))
        {
            // Devolvemos la comida si no hay oro
            AddFood(recipe.foodCost);
            Debug.Log("[Nest] No hay suficiente oro.");
            return false;
        }

        productionQueue.Enqueue((recipe, recipe.productionTime));
        Debug.Log($"[Nest] '{recipe.antName}' añadida a la cola ({productionQueue.Count} en cola).");
        return true;
    }

    void TickProduction()
    {
        if (currentProduction == null)
        {
            if (productionQueue.Count == 0) return;
            var (recipe, time) = productionQueue.Dequeue();
            currentProduction      = recipe;
            currentProductionTimer = time;
        }

        currentProductionTimer -= Time.deltaTime;
        if (currentProductionTimer <= 0f)
        {
            SpawnAnt(currentProduction);
            currentProduction = null;
        }
    }

    void SpawnAnt(AntRecipe recipe)
    {
        if (recipe.prefab == null) { Debug.LogWarning("[Nest] Prefab nulo en receta."); return; }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        var go  = Instantiate(recipe.prefab, pos, Quaternion.identity);
        var ai  = go.GetComponent<InsectAI>();
        if (ai != null)
        {
            ai.antData = recipe.antData;
            RegisterAnt(ai);
        }

        Debug.Log($"[Nest] Hormiga '{recipe.antName}' producida.");
    }

    // ════════════════════════════════════════════
    //  SEGUIMIENTO DE HORMIGAS
    // ════════════════════════════════════════════

    public void RegisterAnt(InsectAI ant)
    {
        if (!activeAnts.Contains(ant))
        {
            activeAnts.Add(ant);
            OnAntListChanged?.Invoke();
        }
    }

    public void UnregisterAnt(InsectAI ant)
    {
        if (activeAnts.Remove(ant))
            OnAntListChanged?.Invoke();
    }

    // ════════════════════════════════════════════
    //  DEBUG
    // ════════════════════════════════════════════
#if UNITY_EDITOR
    [ContextMenu("Debug Estado del Nido")]
    void DebugState()
    {
        Debug.Log($"[Nest] Nivel {Level} | XP {XP}/{XPToNext} | Comida {Food} | Oro {Gold} | Hormigas {activeAnts.Count} | Cola producción {productionQueue.Count}");
    }
#endif
}