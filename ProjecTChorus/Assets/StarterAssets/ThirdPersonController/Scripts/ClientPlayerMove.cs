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
    [Tooltip("O objeto 'PlayerCameraRoot' que estï¿½ dentro deste prefab.")]
    [SerializeField] private Transform m_CameraTarget;

    private CinemachineVirtualCamera sceneVirtualCamera;

    private void Awake()
    {
        // Garante que tudo comeï¿½a desabilitado
        if (m_CharacterController == null)
            m_CharacterController = GetComponent<CharacterController>();

        m_StarterAssetsInputs.enabled = false;
        m_PlayerInput.enabled = false;
        m_ThirdPersonController.enabled = false;
        m_CharacterController.enabled = false;
    }

    // --- Mï¿½TODO ATUALIZADO ---
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Todos (Servidor e Cliente) precisam se inscrever no evento de cena
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

        // E todos precisam rodar a lï¿½gica para a cena ATUAL
        HandleSceneChange(SceneManager.GetActiveScene().name);
    }

    // --- Mï¿½TODO ATUALIZADO ---
    // Agora este mï¿½todo ï¿½ chamado por TODOS (servidor e clientes)
    private void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        HandleSceneChange(sceneName);
    }

    // --- Mï¿½TODO ATUALIZADO ---
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        // Todos precisam se desinscrever do evento
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }


    // --- Lï¿½gica de Habilitaï¿½ï¿½o (Jï¿½ estava correta) ---
    private void HandleSceneChange(string sceneName)
    {
        if (sceneName == "Playground")
        {
            // 1. Inputs: Sï¿½ o Dono (Owner)
            if (IsOwner)
            {
                m_StarterAssetsInputs.enabled = true;
                m_PlayerInput.enabled = true;
                AttemptCameraHook();
                TeleportToSpawn();
                Debug.Log($"[Owner] Controles de input HABILITADOS para {name}");
            }

            // 2. ThirdPersonController (Lï¿½gica de Cï¿½mera/Movimento): Dono E Servidor
            // (O script interno dele jï¿½ tem checagens de IsOwner/IsServer)
            m_ThirdPersonController.enabled = true;

            // 3. CharacterController (Fï¿½sica): Sï¿½ o Servidor
            if (IsServer)
            {
                if (m_CharacterController != null)
                {
                    m_CharacterController.enabled = true;
                    Debug.Log($"[Client/Server] Física (CharacterController) HABILITADA para {name}");
                }
                else
                {
                    Debug.LogWarning($"CharacterController está nulo em {name}!");
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

    // --- Lï¿½gica de Cï¿½mera (Sem mudanï¿½as) ---
    private void AttemptCameraHook()
    {
        sceneVirtualCamera = Object.FindFirstObjectByType<CinemachineVirtualCamera>();
        if (sceneVirtualCamera != null)
        {
            sceneVirtualCamera.Follow = m_CameraTarget;
            sceneVirtualCamera.LookAt = m_CameraTarget;
            Debug.Log($"AttemptCameraHook: Cï¿½mera virtual '{sceneVirtualCamera.name}' foi comandada.");
        }
        else
        {
            Debug.LogWarning("AttemptCameraHook: Nenhuma CinemachineVirtualCamera foi encontrada.");
        }
    }

    // --- NOVO Mï¿½TODO (Apenas movemos o cï¿½digo para cï¿½) ---
    private void TeleportToSpawn()
    {
        Transform spawn = GameObject.Find("SpawnPoint")?.transform;
        if (spawn != null)
        {
            // Lï¿½gica de teleporte segura
            bool ccWasEnabled = m_CharacterController.enabled;
            if (ccWasEnabled) m_CharacterController.enabled = false;

            transform.position = spawn.position;
            transform.rotation = spawn.rotation;

            if (ccWasEnabled) m_CharacterController.enabled = true;

            Debug.Log("Player posicionado no SpawnPoint.");
        }
        else
        {
            Debug.LogWarning("SpawnPoint nï¿½o encontrado na cena 'Playground'.");
        }
    }


    // --- RPC e LateUpdate (Sem mudanï¿½as, jï¿½ estavam corretos) ---
    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, bool jump, bool sprint, float cameraYaw)
    {
        m_StarterAssetsInputs.MoveInput(move);
        m_StarterAssetsInputs.JumpInput(jump);
        m_StarterAssetsInputs.SprintInput(sprint);

        // Esta é a única fonte da verdade para a câmera
        m_ThirdPersonController._cinemachineTargetYaw = cameraYaw;
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;
        if (!m_ThirdPersonController.enabled) return;

        // 1. Cliente (Owner) calcula o ângulo final no TPC.LateUpdate()
        float currentCameraYaw = m_ThirdPersonController._cinemachineTargetYaw;

        // 2. Cliente envia os inputs E o ângulo final para o servidor
        UpdateInputServerRpc(
            m_StarterAssetsInputs.move,
            m_StarterAssetsInputs.jump,
            m_StarterAssetsInputs.sprint,
            currentCameraYaw
        );
    }
}