using UnityEngine;
public enum RewardType {
    Food,           // Comida para la colonia
    ResistanceBonus, // Bonus temporal de resistencia
    ExpBonus,       // Bonus temporal de experiencia
    PoisonImmunity   // Inmunidad temporal al veneno
}

// InsectDataSO.cs — controla los bichos enemigos/presa
[CreateAssetMenu(menuName = "SuperHormiga/InsectData")]
public class InsectDataSO : ScriptableObject
{
    [Header("Movimiento")]
    public float speed       = 1.5f;
    public float aggroRange  = 6f;
    public float attackRange = 0.8f;

    [Header("Combate")]
    public float hp             = 50f;
    public float damage         = 10f;
    public float attackCooldown = 1f;

    [Header("Comportamiento")]
    public float idleWaitTime = 2f;
    public float interactTime = 1.5f;
    public float carryAmount  = 1f;    // unidades de comida que deposita

    [Header("Recompensas al morir (solo enemigos)")]
    public int goldReward;
    public int xpReward;
}

