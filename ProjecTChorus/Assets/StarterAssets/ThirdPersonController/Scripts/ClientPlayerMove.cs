using Unity.Netcode;
using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;
using Cinemachine;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class ClientPlayerMove : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private StarterAssetsInputs m_StarterAssetsInputs;
    [SerializeField] private ThirdPersonController m_ThirdPersonController;
    [SerializeField] private CharacterController m_CharacterController;

    [Header("Camera Target")]
    [Tooltip("O objeto 'PlayerCameraRoot' que est� dentro deste prefab.")]
    [SerializeField] private Transform m_CameraTarget;

    private CinemachineVirtualCamera sceneVirtualCamera;

    private void Awake()
    {
        // Garante que tudo come�a desabilitado
        if (m_CharacterController == null)
            m_CharacterController = GetComponent<CharacterController>();

        m_StarterAssetsInputs.enabled = false;
        m_PlayerInput.enabled = false;
        m_ThirdPersonController.enabled = false;
        m_CharacterController.enabled = false;
    }

    // --- M�TODO ATUALIZADO ---
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Todos (Servidor e Cliente) precisam se inscrever no evento de cena
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

        // E todos precisam rodar a l�gica para a cena ATUAL
        HandleSceneChange(SceneManager.GetActiveScene().name);
    }

    // --- M�TODO ATUALIZADO ---
    // Agora este m�todo � chamado por TODOS (servidor e clientes)
    private void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        HandleSceneChange(sceneName);
    }

    // --- M�TODO ATUALIZADO ---
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        // Todos precisam se desinscrever do evento
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }


    // --- L�gica de Habilita��o (J� estava correta) ---
    private void HandleSceneChange(string sceneName)
    {
        if (sceneName == "Playground")
        {
            // 1. Inputs: S� o Dono (Owner)
            if (IsOwner)
            {
                m_StarterAssetsInputs.enabled = true;
                m_PlayerInput.enabled = true;
                AttemptCameraHook();
                TeleportToSpawn();
                Debug.Log($"[Owner] Controles de input HABILITADOS para {name}");
            }

            // 2. ThirdPersonController (L�gica de C�mera/Movimento): Dono E Servidor
            // (O script interno dele j� tem checagens de IsOwner/IsServer)
            m_ThirdPersonController.enabled = true;

            // 3. CharacterController (F�sica): S� o Servidor
            if (IsServer)
            {
                if (m_CharacterController != null)
                {
                    m_CharacterController.enabled = true;
                    Debug.Log($"[Server] F�sica (CharacterController) HABILITADA para {name}");
                }
            }
        }
        else // Se for a cena "Lobby" ou qualquer outra
        {
            // Desabilita TUDO no Lobby
            m_StarterAssetsInputs.enabled = false;
            m_PlayerInput.enabled = false;
            m_ThirdPersonController.enabled = false;
            if (m_CharacterController != null)
                m_CharacterController.enabled = false;
        }
    }

    // --- L�gica de C�mera (Sem mudan�as) ---
    private void AttemptCameraHook()
    {
        sceneVirtualCamera = Object.FindFirstObjectByType<CinemachineVirtualCamera>();
        if (sceneVirtualCamera != null)
        {
            sceneVirtualCamera.Follow = m_CameraTarget;
            sceneVirtualCamera.LookAt = m_CameraTarget;
            Debug.Log($"AttemptCameraHook: C�mera virtual '{sceneVirtualCamera.name}' foi comandada.");
        }
        else
        {
            Debug.LogWarning("AttemptCameraHook: Nenhuma CinemachineVirtualCamera foi encontrada.");
        }
    }

    // --- NOVO M�TODO (Apenas movemos o c�digo para c�) ---
    private void TeleportToSpawn()
    {
        Transform spawn = GameObject.Find("SpawnPoint")?.transform;
        if (spawn != null)
        {
            // L�gica de teleporte segura
            bool ccWasEnabled = m_CharacterController.enabled;
            if (ccWasEnabled) m_CharacterController.enabled = false;

            transform.position = spawn.position;
            transform.rotation = spawn.rotation;

            if (ccWasEnabled) m_CharacterController.enabled = true;

            Debug.Log("Player posicionado no SpawnPoint.");
        }
        else
        {
            Debug.LogWarning("SpawnPoint n�o encontrado na cena 'Playground'.");
        }
    }


    // --- RPC e LateUpdate (Sem mudan�as, j� estavam corretos) ---
    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, Vector2 look, bool jump, bool sprint, float cameraYaw)
    {
        m_StarterAssetsInputs.MoveInput(move);
        m_StarterAssetsInputs.LookInput(look);
        m_StarterAssetsInputs.JumpInput(jump);
        m_StarterAssetsInputs.SprintInput(sprint);
        m_ThirdPersonController._cinemachineTargetYaw = cameraYaw;
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;
        if (!m_ThirdPersonController.enabled) return;

        float currentCameraYaw = m_ThirdPersonController._cinemachineTargetYaw;

        UpdateInputServerRpc(
            m_StarterAssetsInputs.move,
            m_StarterAssetsInputs.look,
            m_StarterAssetsInputs.jump,
            m_StarterAssetsInputs.sprint,
            currentCameraYaw
        );
    }
}