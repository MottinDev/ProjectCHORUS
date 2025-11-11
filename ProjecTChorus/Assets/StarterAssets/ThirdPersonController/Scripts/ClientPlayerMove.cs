using Unity.Netcode;
using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;
using Cinemachine;
using Unity.Collections; // Necessário para OnSceneLoaded
using System.Collections.Generic; // Necessário para OnSceneLoaded
using UnityEngine.SceneManagement; // Necessário para LoadSceneMode

public class ClientPlayerMove : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private StarterAssetsInputs m_StarterAssetsInputs;
    [SerializeField] private ThirdPersonController m_ThirdPersonController;

    [Header("Camera Target")]
    [Tooltip("O objeto 'PlayerCameraRoot' que está dentro deste prefab.")]
    [SerializeField] private Transform m_CameraTarget;

    private CinemachineVirtualCamera sceneVirtualCamera;
    private bool sceneLoaded = false;


    private void Awake()
    {
        // tudo começa desabilitado
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
            m_ThirdPersonController.enabled = true; // TPC Habilitado para Owner (Câmera)

            // --- NOVA LÓGICA DE CÂMERA ---
            // 1. "Inscreve-se" no evento de carregamento de cena do NetworkManager
            //    Isso garante que vamos re-verificar a câmera CADA vez que uma cena carregar.
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

            // 2. Tenta pegar a câmera imediatamente (caso já estejamos na cena certa)
            AttemptCameraHook();
        }

        // Lógica do Servidor
        // O servidor é o único que pode rodar a lógica de movimento.
        if (IsServer)
        {
            m_ThirdPersonController.enabled = true;
        }
    }

    // --- NOVO MÉTODO ---
    // Este método é chamado pelo evento OnLoadEventCompleted
    private void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsOwner) return;

        // Marca que a cena está 100% carregada
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
            Debug.LogWarning("SpawnPoint não encontrado na cena carregada.");
        }


        // Agora sim ativa o movimento
        m_StarterAssetsInputs.enabled = true;
        m_PlayerInput.enabled = true;
        m_ThirdPersonController.enabled = true;

        AttemptCameraHook(); // agora funciona

        Debug.Log("Cena carregada completamente. Player ativado.");
    }


    // --- NOVO MÉTODO ---
    // Esta é a sua lógica de câmera original, agora em um método reutilizável
    private void AttemptCameraHook()
    {
        // Tenta encontrar a câmera na cena ATUAL
        // Usamos FindFirstObjectByType (novo) em vez de FindObjectOfType (antigo)
        sceneVirtualCamera = Object.FindFirstObjectByType<CinemachineVirtualCamera>();

        if (sceneVirtualCamera != null)
        {
            // Se encontrou, comanda a câmera
            sceneVirtualCamera.Follow = m_CameraTarget;
            sceneVirtualCamera.LookAt = m_CameraTarget;
            Debug.Log($"AttemptCameraHook: Câmera virtual '{sceneVirtualCamera.name}' foi comandada para seguir {m_CameraTarget.name}.");
        }
        else
        {
            // Se não encontrou, avisa no console.
            // Note: Isso NÃO é mais um LogError, pois é
            // ESPERADO que não haja câmera na Cena de Lobby.
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


    // 4. RPC e LateUpdate (Sem alterações aqui)
    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, Vector2 look, bool jump, bool sprint, float cameraYaw)
    {
        // 1. Aplica os inputs de movimento
        m_StarterAssetsInputs.MoveInput(move);
        m_StarterAssetsInputs.LookInput(look);
        m_StarterAssetsInputs.JumpInput(jump);
        m_StarterAssetsInputs.SprintInput(sprint);

        // 2. Removemos o LookInput(look)
        // 3. Esta é a nova "Fonte da Verdade" para o movimento do servidor.
        m_ThirdPersonController._cinemachineTargetYaw = cameraYaw;
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;

        // O TPC.LateUpdate (que roda no Owner) acabou de rodar
        // e calculou o ângulo final da câmera.
        // Nós o lemos.
        float currentCameraYaw = m_ThirdPersonController._cinemachineTargetYaw;

        // Envia os inputs de movimento E o ângulo final da câmera
        UpdateInputServerRpc(
            m_StarterAssetsInputs.move,
            m_StarterAssetsInputs.look,
            m_StarterAssetsInputs.jump,
            m_StarterAssetsInputs.sprint,
            currentCameraYaw
        );
    }
}