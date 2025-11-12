using UnityEngine;
using Unity.Netcode;

// MUDANÇA 1: Mude de MonoBehaviour para NetworkBehaviour
public class ButtonTrigger : NetworkBehaviour
{
    [SerializeField] private string buttonID;
    private DoorManager doorManager;

    // A flag 'hasBeenTriggered' não é mais necessária.
    // O DoorManager já tem um timer de timeout, que é a fonte da verdade.

    // MUDANÇA 2: Use OnNetworkSpawn para encontrar o DoorManager
    public override void OnNetworkSpawn()
    {
        // Apenas o servidor precisa de uma referência ao DoorManager
        if (IsServer)
        {
            doorManager = Object.FindFirstObjectByType<DoorManager>();
            if (doorManager == null)
            {
                Debug.LogError($"[ButtonTrigger {buttonID}] NÃO ENCONTROU O DoorManager!");
            }
        }
    }

    // MUDANÇA 3: A lógica inteira agora só roda no SERVIDOR
    private void OnTriggerStay(Collider other)
    {
        // Se não formos o servidor, não fazemos nada.
        if (!IsServer) return;

        // Se a porta já estiver aberta, não fazemos nada.
        if (doorManager.IsOpen == true) return;

        // Checa se quem entrou é um jogador (tem CharacterController)
        // Não precisamos checar 'IsOwner', pois o servidor detecta
        // a colisão tanto do P1 (Owner) quanto do P2 (Fantasma).
        if (other.GetComponent<CharacterController>() != null)
        {
            // É um jogador!
            if (doorManager != null)
            {
                // MUDANÇA 4: Chamamos o RPC diretamente.
                // Como já estamos no servidor, isso é uma chamada de função local.
                // O servidor está "apertando o botão" em nome do jogador.
                doorManager.PressButtonServerRpc(buttonID);
            }
        }
    }

    // O OnTriggerExit não é mais necessário.
}