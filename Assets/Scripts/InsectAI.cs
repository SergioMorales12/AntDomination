using UnityEngine;

public class InsectAI : MonoBehaviour
{
    public InsectState currentState = InsectState.Idle;

    void Update()
    {
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

    // --- AQUÍ VAN LAS FUNCIONES DE CADA ESTADO ---

    void UpdateIdle()
    {
        // Lógica: Esperar unos segundos y luego cambiar a Walking
        // if (tiempoPasado) ChangeState(InsectState.Walking);
    }

    void UpdateWalking()
    {
        // Lógica: Moverse hacia un objetivo usando NavMesh o transform.Translate
        // if (veUnEnemigo) ChangeState(InsectState.Attacking);
    }

    void UpdateGrabbed()
    {
        // Lógica: Quedarse quieto, reproducir animación de patalear.
        // No hacer nada más, el jugador controla la posición.
    }

    void UpdateDead()
    {
        // Lógica: Quedarse en el suelo. Quizás encogerse y desaparecer (Destroy).
    }
    
    // (Añade el resto de funciones: UpdateInteracting, UpdateAttacking, etc.)
}