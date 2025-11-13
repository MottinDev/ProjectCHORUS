using Unity.Netcode;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Collections;

public class PlayerData : NetworkBehaviour
{



    public NetworkVariable<FixedString64Bytes> PlayerNick = new NetworkVariable<FixedString64Bytes>(
         "Jogador",
         NetworkVariableReadPermission.Everyone,
         NetworkVariableWritePermission.Server
     );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // 1. Cliente lê o nick da ponte
            string myNick = PlayerNickBridge.NickToCarry;

            if (string.IsNullOrEmpty(myNick))
            {
                myNick = "Jogador (Falha Bridge)";
                Debug.LogError("[PlayerData] Nick da 'Ponte' estava VAZIO! Isso é um bug?");
            }

            // 2. Cliente envia o nick ao servidor
            Debug.Log($"[PlayerData] (Cliente) Lendo nick '{myNick}' e enviando ao servidor.");
            SetNickServerRpc(myNick);
        }
    }

    // O RPC EM SI: Não é async! Ele é um "void" simples.
    [ServerRpc]
    private void SetNickServerRpc(string nick, ServerRpcParams rParams = default)
    {
        // 3. Servidor recebe o RPC
        Debug.Log($"[PlayerData] (Servidor) RPC SetNickServerRpc recebido para {rParams.Receive.SenderClientId} com nick '{nick}'.");

        PlayerNick.Value = nick;

        // 4. O Servidor AGORA chama o helper "dispare-e-esqueça".
        // O RPC vai terminar, mas este método async continuará rodando em background.
        RegisterWithChatManagerAsync(rParams.Receive.SenderClientId, this);
    }

    // O HELPER: Este é async e vai esperar o ChatManager.
    private async void RegisterWithChatManagerAsync(ulong netcodeClientId, PlayerData playerData)
    {
        Debug.Log($"[PlayerData] (Servidor) Helper RegisterWithChatManagerAsync iniciado para {netcodeClientId}.");

        // 5. O helper espera, frame a frame, até o Singleton estar pronto.
        while (ChatManager.Instance == null)
        {
            Debug.LogWarning($"[PlayerData] (Servidor) ChatManager.Instance é NULO. Esperando 1 frame... (Cliente: {netcodeClientId})");
            await Task.Yield(); // Pausa a execução e continua no próximo frame
        }

        // 6. O Singleton foi encontrado!
        Debug.Log($"[PlayerData] (Servidor) ChatManager.Instance encontrado! Registrando {netcodeClientId}...");
        ChatManager.Instance.RegisterPlayer(netcodeClientId, this);
    }
}