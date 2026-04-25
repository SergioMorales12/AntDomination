using UnityEngine;

public enum AntRole
{
    Worker,     // Obrera: Recolecta comida, construye el hormiguero
    Soldier,    // Soldado: Defiende el hormiguero, ataca a enemigos
}
[CreateAssetMenu(menuName = "SuperHormiga/AntData")]
public class AntDataSO : ScriptableObject {
    public AntRole role;          // Obrera, Soldado, Exploradora...
    public float speed, hp, damage, attackRange;
    public float hpFleeThreshold; // huye si hp < X%

    [Range(0,1)] public float fightWeight;   // 0 = nunca pelea, 1 = busca pelea
    [Range(0,1)] public float forageWeight;  // 0 = no recoge, 1 = prioridad recolectar
    [Range(0,1)] public float buildWeight;
}