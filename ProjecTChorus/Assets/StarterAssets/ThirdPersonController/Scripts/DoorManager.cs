using Unity.Netcode;
using UnityEngine;

public class DoorManager : NetworkBehaviour
{
    [SerializeField] private GameObject porta;

    // "Fonte da Verdade" para o estado da porta
    // Assim, quem entrar depois (late-joiner) vê a porta aberta.
    private NetworkVariable<bool> isDoorOpen = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Estados dos botões (só o servidor se importa com isso)
    private bool isButtonAPressed = false;
    private bool isButtonBPressed = false;
    private float buttonTimeout = 10.0f; // 2 segundos para apertarem
    private float buttonATimer = 0f;
    private float buttonBTimer = 0f;

    public override void OnNetworkSpawn()
    {
        // Cliente: Escuta a mudança de estado da porta
        isDoorOpen.OnValueChanged += (bool prev, bool current) =>
        {
            // Abre/fecha a porta localmente
            porta.SetActive(!current);
        };

        // Garante o estado inicial
        porta.SetActive(!isDoorOpen.Value);
    }

    private void Update()
    {
        // Este Update só precisa rodar no servidor
        if (!IsServer) return;

        // Lógica de timeout
        if (isButtonAPressed)
        {
            buttonATimer -= Time.deltaTime;
            if (buttonATimer <= 0) isButtonAPressed = false;
        }
        if (isButtonBPressed)
        {
            buttonBTimer -= Time.deltaTime;
            if (buttonBTimer <= 0) isButtonBPressed = false;
        }
    }

    // Os jogadores vão chamar isso via RPC
    [ServerRpc(RequireOwnership = false)]
    public void PressButtonServerRpc(string buttonID)
    {
        if (isDoorOpen.Value) return; // Porta já está aberta

        if (buttonID == "A")
        {
            isButtonAPressed = true;
            buttonATimer = buttonTimeout;
        }
        else if (buttonID == "B")
        {
            isButtonBPressed = true;
            buttonBTimer = buttonTimeout;
        }

        // A Mágica: Checa a condição de sincronia
        if (isButtonAPressed && isButtonBPressed)
        {
            Debug.Log("Sincronização OK! Abrindo a porta.");
            isDoorOpen.Value = true; // Isso vai replicar para todos
        }
    }
}