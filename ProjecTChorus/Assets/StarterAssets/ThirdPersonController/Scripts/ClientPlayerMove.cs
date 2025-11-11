using Unity.Netcode;
using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;
using Cinemachine;
using Unity.Collections; // Necess�rio para OnSceneLoaded
using System.Collections.Generic; // Necess�rio para OnSceneLoaded
using UnityEngine.SceneManagement; // Necess�rio para LoadSceneMode

public class ClientPlayerMove : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private StarterAssetsInputs m_StarterAssetsInputs;
    [SerializeField] private ThirdPersonController m_ThirdPersonController;

    [Header("Camera Target")]
    [Tooltip("O objeto 'PlayerCameraRoot' que est� dentro deste prefab.")]
    [SerializeField] private Transform m_CameraTarget;

    private CinemachineVirtualCamera sceneVirtualCamera;
    private bool sceneLoaded = false;


    private void Awake()
    {
        // tudo come�a desabilitado
        m_StarterAssetsInputs.enabled = false;
        m_PlayerInput.enabled = false;
        m_ThirdPersonController.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Habilita os scripts de input para o jogador local
            m_StarterAssetsInputs.enabled = true;
            m_PlayerInput.enabled = true;
            m_ThirdPersonController.enabled = true; // TPC Habilitado para Owner (C�mera)

            // --- NOVA L�GICA DE C�MERA ---
            // 1. "Inscreve-se" no evento de carregamento de cena do NetworkManager
            //    Isso garante que vamos re-verificar a c�mera CADA vez que uma cena carregar.
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

            // 2. Tenta pegar a c�mera imediatamente (caso j� estejamos na cena certa)
            AttemptCameraHook();
        }

        // L�gica do Servidor
        // O servidor � o �nico que pode rodar a l�gica de movimento.
        if (IsServer)
        {
            m_ThirdPersonController.enabled = true;
        }
    }

    // --- NOVO M�TODO ---
    // Este m�todo � chamado pelo evento OnLoadEventCompleted
    private void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsOwner) return;

        // Marca que a cena est� 100% carregada
        sceneLoaded = true;

        Transform spawn = GameObject.Find("SpawnPoint")?.transform;

        if (spawn != null)
        {
            transform.position = spawn.position;
            transform.rotation = spawn.rotation;
            Debug.Log("Player posicionado no SpawnPoint.");
        }
        else
        {
            Debug.LogWarning("SpawnPoint n�o encontrado na cena carregada.");
        }


        // Agora sim ativa o movimento
        m_StarterAssetsInputs.enabled = true;
        m_PlayerInput.enabled = true;
        m_ThirdPersonController.enabled = true;

        AttemptCameraHook(); // agora funciona

        Debug.Log("Cena carregada completamente. Player ativado.");
    }


    // --- NOVO M�TODO ---
    // Esta � a sua l�gica de c�mera original, agora em um m�todo reutiliz�vel
    private void AttemptCameraHook()
    {
        // Tenta encontrar a c�mera na cena ATUAL
        // Usamos FindFirstObjectByType (novo) em vez de FindObjectOfType (antigo)
        sceneVirtualCamera = Object.FindFirstObjectByType<CinemachineVirtualCamera>();

        if (sceneVirtualCamera != null)
        {
            // Se encontrou, comanda a c�mera
            sceneVirtualCamera.Follow = m_CameraTarget;
            sceneVirtualCamera.LookAt = m_CameraTarget;
            Debug.Log($"AttemptCameraHook: C�mera virtual '{sceneVirtualCamera.name}' foi comandada para seguir {m_CameraTarget.name}.");
        }
        else
        {
            // Se n�o encontrou, avisa no console.
            // Note: Isso N�O � mais um LogError, pois �
            // ESPERADO que n�o haja c�mera na Cena de Lobby.
            Debug.LogWarning("AttemptCameraHook: Nenhuma CinemachineVirtualCamera foi encontrada na cena atual.");
        }
    }

    // --- IMPORTANTE: LIMPEZA ---
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // "Desinscreve-se" do evento para evitar erros
        if (IsOwner)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            }
        }
    }


    // 4. RPC e LateUpdate (Sem altera��es aqui)
    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, Vector2 look, bool jump, bool sprint, float cameraYaw)
    {
        // 1. Aplica os inputs de movimento
        m_StarterAssetsInputs.MoveInput(move);
        m_StarterAssetsInputs.LookInput(look);
        m_StarterAssetsInputs.JumpInput(jump);
        m_StarterAssetsInputs.SprintInput(sprint);

        // 2. Removemos o LookInput(look)
        // 3. Esta � a nova "Fonte da Verdade" para o movimento do servidor.
        m_ThirdPersonController._cinemachineTargetYaw = cameraYaw;
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        // O TPC.LateUpdate (que roda no Owner) acabou de rodar
        // e calculou o �ngulo final da c�mera.
        // N�s o lemos.
        float currentCameraYaw = m_ThirdPersonController._cinemachineTargetYaw;

        // Envia os inputs de movimento E o �ngulo final da c�mera
        UpdateInputServerRpc(
            m_StarterAssetsInputs.move,
            m_StarterAssetsInputs.look,
            m_StarterAssetsInputs.jump,
            m_StarterAssetsInputs.sprint,
            currentCameraYaw
        );
    }
}