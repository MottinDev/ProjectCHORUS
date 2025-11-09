using Unity.Netcode;
using UnityEngine;
using StarterAssets; 
using UnityEngine.InputSystem;
using Cinemachine; 

public class ClientPlayerMove : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerInput m_PlayerInput;
    [SerializeField] private StarterAssetsInputs m_StarterAssetsInputs;
    [SerializeField] private ThirdPersonController m_ThirdPersonController;

    //  Variáveis de câmera 
    [Header("Camera Target")]
    [Tooltip("O objeto 'PlayerCameraRoot' que está dentro deste prefab.")]
    [SerializeField] private Transform m_CameraTarget;

    // Variável para guardar a VCam da cena (para performance)
    private CinemachineVirtualCamera sceneVirtualCamera;


    private void Awake()
    {
        //  tudo começa desabilitado
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

            // Habilita o TPC para o Owner (para câmera e lógica de input)
            m_ThirdPersonController.enabled = true;

            // --- LÓGICA DE CÂMERA ---

            // 1. Encontra a câmera virtual na CENA
            //    (FindObjectOfType é ok no Spawn, pois só roda uma vez)
            if (sceneVirtualCamera == null)
            {
                sceneVirtualCamera = Object.FindFirstObjectByType<CinemachineVirtualCamera>();

            }

            // 2. Se encontrou a câmera...
            if (sceneVirtualCamera != null)
            {
                // 3. Comanda a câmera da cena para SEGUIR e OLHAR para o nosso alvo
                sceneVirtualCamera.Follow = m_CameraTarget;
                sceneVirtualCamera.LookAt = m_CameraTarget;

                Debug.Log($"OnNetworkSpawn: Câmera virtual '{sceneVirtualCamera.name}' foi comandada para seguir {m_CameraTarget.name}.");
            }
            else
            {
                Debug.LogError("PlayerFollowCamera (CinemachineVirtualCamera) não foi encontrada na cena!");
            }
            
        }

        // Lógica do Servidor 
        // O servidor é o único que pode rodar a lógica de movimento.
        if (IsServer)
        {
            m_ThirdPersonController.enabled = true;
        }
    }

    // 4. RPC e LateUpdate 
    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, Vector2 look, bool jump, bool sprint)
    {
        m_StarterAssetsInputs.MoveInput(move);
        m_StarterAssetsInputs.LookInput(look);
        m_StarterAssetsInputs.JumpInput(jump);
        m_StarterAssetsInputs.SprintInput(sprint);
    }


    private void LateUpdate()
    {
        if (!IsOwner)
            return;

        // Envia os inputs para o servidor
        UpdateInputServerRpc(m_StarterAssetsInputs.move, m_StarterAssetsInputs.look, m_StarterAssetsInputs.jump, m_StarterAssetsInputs.sprint);
    }
}