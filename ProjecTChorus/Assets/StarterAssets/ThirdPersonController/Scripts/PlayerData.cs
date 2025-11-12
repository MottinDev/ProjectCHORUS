using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking; 
using System.Text;           
using System.Threading.Tasks;

public class PlayerData : NetworkBehaviour
{
    public NetworkVariable<string> PlayerNick = new NetworkVariable<string>(
        "Jogador",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // --- Configuração da API Customizada ---

    // ⚠️ Chave secreta da nossa API (deve ser a MESMA do arquivo .env)
    private string m_ApiSecretKey = "MEU_SEGREDO_SUPER_SEGURO_DO_UNITY_12345";

    // URL da API (rodando localmente no Docker)
    // Se o servidor Unity rodar em um PC e o Docker em outro, 
    // troque 'localhost' pelo IP da máquina do Docker.
    private string m_ApiBaseUrl = "http://44.203.6.233";

    // Classe auxiliar para parsear a resposta JSON da API
    [System.Serializable]
    private class NickResponse
    {
        public string nick;
    }

    // --- Lógica de Spawn e RPCs (Idêntica) ---

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            string authId = AuthenticationService.Instance.PlayerId;
            // Pede ao servidor para carregar o nick usando nosso AuthID
            RequestNickLoadServerRpc(authId);
        }
    }

    // O cliente chama isso
    [ServerRpc]
    private void RequestNickLoadServerRpc(string authPlayerId, ServerRpcParams rParams = default)
    {
        // O servidor recebe o AuthID e começa a carregar
        LoadNickFromApi(rParams.Receive.SenderClientId, authPlayerId);
    }

    // --- LÓGICA DE CARREGAR (Modificada) ---

    // Servidor executa isso
    private async void LoadNickFromApi(ulong netcodeClientId, string authPlayerId)
    {
        if (!IsServer) return;

        string url = $"{m_ApiBaseUrl}/player/{authPlayerId}/nick";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Adiciona o cabeçalho de autorização
            request.SetRequestHeader("Authorization", $"Bearer {m_ApiSecretKey}");

            try
            {
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Sucesso, nick encontrado (Código 200 OK)
                    string jsonResponse = request.downloadHandler.text;
                    NickResponse data = JsonUtility.FromJson<NickResponse>(jsonResponse);

                    PlayerNick.Value = data.nick;
                    Debug.Log($"[Server] Nick '{PlayerNick.Value}' carregado da API para {netcodeClientId}");
                }
                else if (request.responseCode == 404)
                {
                    // Nick não encontrado (jogador novo)
                    PlayerNick.Value = $"Jogador {netcodeClientId}";
                    Debug.Log($"[Server] Nick não encontrado na API para {netcodeClientId}. Usando default.");
                }
                else
                {
                    // Outro erro de API
                    throw new System.Exception($"Erro da API: {request.responseCode} - {request.error}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Server] Falha ao carregar nick da API para {netcodeClientId}: {e.Message}");
                PlayerNick.Value = $"Jogador {netcodeClientId}";
            }
        }
    }


    // --- LÓGICA DE SALVAR (Modificada) ---

    // O Cliente chama isso
    [ServerRpc]
    public void SetNickServerRpc(string newNick, string authPlayerId, ServerRpcParams rParams = default)
    {
        // Servidor recebe e salva
        SaveNickToApi(newNick, authPlayerId, rParams.Receive.SenderClientId);
    }

    // Servidor executa isso
    private async void SaveNickToApi(string newNick, string authPlayerId, ulong netcodeClientId)
    {
        if (!IsServer) return;

        string url = $"{m_ApiBaseUrl}/player/{authPlayerId}/nick";

        // Criar o corpo (payload) JSON: {"nick":"NovoNick"}
        string jsonPayload = $"{{\"nick\":\"{newNick}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        // Usamos 'using' para garantir que o request seja descartado
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            // Configurar o Upload Handler (para enviar dados)
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // Configurar o Download Handler (para receber a resposta "OK")
            request.downloadHandler = new DownloadHandlerBuffer();

            // Definir cabeçalhos OBRIGATÓRIOS
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {m_ApiSecretKey}");

            try
            {
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Sucesso (Código 200 OK)
                    PlayerNick.Value = newNick;
                    Debug.Log($"[Server] Nick '{newNick}' salvo na API para {netcodeClientId}");
                }
                else
                {
                    // Erro
                    throw new System.Exception($"Erro da API ao salvar: {request.responseCode} - {request.error}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Server] Falha ao salvar nick na API para {netcodeClientId}: {e.Message}");
            }
        }
    }
}