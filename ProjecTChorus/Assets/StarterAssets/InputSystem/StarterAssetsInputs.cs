using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Esta versão é mais simples. Ela apenas obedece a flag 'isChatting'.
// O ChatManager é quem vai definir essa flag.
public class StarterAssetsInputs : NetworkBehaviour
{
    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool sprint;

    [Header("Chat Mode")]
    public bool isChatting = false; // O ChatManager vai controlar isso

    [Header("Movement Settings")]
    public bool analogMovement;

    [Header("Mouse Cursor Settings")]
    public bool cursorLocked = true;
    public bool cursorInputForLook = true;

 

    // O Update AGORA SÓ SERVE PARA O INPUT DE TECLADO
    // Removemos a detecção do "Enter" daqui para matar a "race condition".
    private void Update()
    {
        if (!IsOwner) return;

        // A lógica de "Enter" foi movida para o ChatManager para centralizar o controle
    }

    // --- MÉTODOS DE INPUT ---
    // A lógica 'if (isChatting)' aqui dentro é a parte mais importante.

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value)
    {
        MoveInput(value.Get<Vector2>());
    }

    public void OnLook(InputValue value)
    {
        if (cursorInputForLook)
        {
            LookInput(value.Get<Vector2>());
        }
    }

    public void OnJump(InputValue value)
    {
        JumpInput(value.isPressed);
    }

    public void OnSprint(InputValue value)
    {
        SprintInput(value.isPressed);
    }
#endif

    public void MoveInput(Vector2 newMoveDirection)
    {
        if (isChatting)
        {
            move = Vector2.zero;
            return;
        }
        move = newMoveDirection;
    }

    public void LookInput(Vector2 newLookDirection)
    {
        if (isChatting)
        {
            look = Vector2.zero;
            return;
        }
        look = newLookDirection;
    }

    public void JumpInput(bool newJumpState)
    {
        if (isChatting) return;
        jump = newJumpState;
    }

    public void SprintInput(bool newSprintState)
    {
        if (isChatting) return;
        sprint = newSprintState;
    }

    // --- CONTROLE DO CURSOR ---
    private void OnApplicationFocus(bool hasFocus)
    {
        // <-- MUDANÇA CRÍTICA -->
        // Se este componente de script não estiver habilitado,
        // não faça absolutamente nada.
        if (!enabled)
        {
            return;
        }

        // Se o script ESTIVER habilitado (ex: no Playground),
        // a lógica antiga funciona.
        if (!isChatting)
        {
            SetCursorState(cursorLocked);
        }
    }

    public void SetCursorState(bool newState)
    {
        Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
    }
}