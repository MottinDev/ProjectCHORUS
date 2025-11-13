using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;

public class ChatManager : NetworkBehaviour
{
    //LÓGICA DO SINGLETON
    public static ChatManager Instance { get; private set; }

    [Header("UI Elements")]
    public TMP_Text chatLogText;
    public TMP_InputField chatInputField;
    public Button sendButton;
    // Este mapa só existirá no servidor e liga o NetcodeID ao PlayerData.
    private Dictionary<ulong, PlayerData> m_PlayerDataMap = new Dictionary<ulong, PlayerData>();

    [Header("Fade Logic")]
    public CanvasGroup chatCanvasGroup;
    public float inactivityTimeout = 10.0f;
    public float fadeDuration = 0.5f;

    private Coroutine fadeCoroutine;
    private StarterAssetsInputs localPlayerInputs;

    // O Relógio Lógico do Servidor (Começa em 0)
    private int serverLogicalClock = 0;

    // --- Config da API (somente nick e message_text) ---
    [Header("Custom API Config")]
    [SerializeField] private string apiSecretKey = "MEU_SEGREDO_SUPER_SEGURO_DO_UNITY_12345";

#if UNITY_SERVER
    [SerializeField] private string apiBaseUrl = "http://localhost:5000";
#else
    [SerializeField] private string apiBaseUrl = "http://3.222.116.91";
#endif

    [System.Serializable]
    private class ChatPostBody
    {
        public string nick;
        public string message_text;
    }

    [System.Serializable]
    private class ChatMessageRow
    {
        public string nick;
        public string message_text;
        public string timestamp;
    }

    [System.Serializable]
    private class ChatMessageListWrapper
    {
        public List<ChatMessageRow> items;
    }

    private void Awake()
    {
        // Garante que só existe um ChatManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSend);
        }
        if (chatInputField != null)
        {
            chatInputField.onSubmit.AddListener(OnSubmitChat);
            chatInputField.onSelect.AddListener(OnSelectChat);
            // 'onDeselect' continua comentado para corrigir o bug da scrollbar
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
        if (chatCanvasGroup != null)
        {
            chatCanvasGroup.alpha = 0f;
        }

        // Cliente carrega histórico ao entrar
        if (IsClient)
        {
            _ = FetchAndShowHistoryAsync();
        }
    }

    // --- RPCs e Handlers de Conexão  ---
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        //serverLogicalClock++; // Relógio avança
        //BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} entrou na sala.", serverLogicalClock, default);

        // NÃO ENVIE A MENSAGEM AQUI.
        // É muito cedo, o PlayerData ainda não se registrou e não temos o nick.
        // A mensagem será enviada pelo RegisterPlayer().
        Debug.Log($"[ChatManager] Cliente {clientId} conectou. Aguardando registro do PlayerData.");

        // (Remova o serverLogicalClock++ e o BroadcastMessageClientRpc daqui)
    }
    //private void HandleClientDisconnected(ulong clientId)
    //{

    //    if (m_PlayerDataMap.ContainsKey(clientId))
    //    {
    //        m_PlayerDataMap.Remove(clientId);
    //        Debug.Log($"[ChatManager] Desregistrou PlayerData para {clientId}.");
    //    }

    //    serverLogicalClock++; // Relógio avança
    //    BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} saiu na sala.", serverLogicalClock, default);
    //}

    private void HandleClientDisconnected(ulong clientId)
    {
        string nick = $"Jogador {clientId}"; // Nick padrão caso algo falhe

        // 1. Tenta pegar o nick ANTES de remover o jogador do mapa
        if (m_PlayerDataMap.TryGetValue(clientId, out PlayerData playerData))
        {
            nick = playerData.PlayerNick.Value.ToString(); // Pega o nick!
            m_PlayerDataMap.Remove(clientId);
            Debug.Log($"[ChatManager] Desregistrou PlayerData para {clientId} (Nick: {nick}).");
        }
        else
        {
            Debug.LogWarning($"[ChatManager] Cliente {clientId} desconectou, mas não estava no PlayerDataMap.");
        }

        // (O 'if (m_PlayerDataMap.ContainsKey(clientId))' original foi substituído pela lógica acima)

        serverLogicalClock++; // Relógio avança

        // 2. Usa o nick na mensagem
        BroadcastMessageClientRpc($"[SISTEMA]: {nick} saiu da sala.", serverLogicalClock, default);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitChatMessageServerRpc(string message, ServerRpcParams rParams = default)
    {
        ulong senderId = rParams.Receive.SenderClientId;
        string nick = $"Jogador {senderId}"; // Um nick padrão caso algo falhe
        if (m_PlayerDataMap.TryGetValue(senderId, out PlayerData playerData))
        {
            // Se conseguir, lê o valor ATUAL da NetworkVariable
            nick = playerData.PlayerNick.Value.ToString();
        }
        string fMessage = $"[{nick}]: {message}";
        serverLogicalClock++; // Relógio avança

        // Broadcast no jogo
        BroadcastMessageClientRpc(fMessage, serverLogicalClock, default);

        // Persistência (somente mensagens públicas; evita sistema/privado)
        _ = PersistPublicMessageAsync(nick, message);
    }

    [ClientRpc]
    private void BroadcastMessageClientRpc(string message, int timestamp, ClientRpcParams clientRpcParams = default)
    {
        // Agora o ClientRpc repassa o timestamp
        AddMessageToLog(message, timestamp);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPrivateMessageServerRpc(string targetNick, string message, ServerRpcParams rParams = default)
    {
        ulong senderId = rParams.Receive.SenderClientId;
        string senderNick = $"Jogador {senderId}"; // Nick padrão do remetente

        // 1. Encontra o nick do REMETENTE (igual antes)
        if (m_PlayerDataMap.TryGetValue(senderId, out PlayerData senderData))
        {
            senderNick = senderData.PlayerNick.Value.ToString();
        }

        // 2. Encontra o ID do DESTINATÁRIO (A NOVA LÓGICA)
        ulong? targetClientId = null; // Usamos '?' (Nullable) para saber se encontramos

        // Itera por todo o dicionário de jogadores no servidor
        foreach (var entry in m_PlayerDataMap)
        {
            if (entry.Value.PlayerNick.Value.ToString() == targetNick)
            {
                targetClientId = entry.Key; // Encontramos! Guardamos o ID.
                break; // Para o loop
            }
        }

        // 3. Processa o resultado
        if (targetClientId.HasValue) // Se encontramos (targetClientId != null)
        {
            // SUCESSO: Encontramos o jogador, lógica de envio normal
            string fMessage = $"[PRIVADO de {senderNick}]: {message}";

            ulong[] targetClientIds = new ulong[] {
                senderId, 			 // O remetente
				targetClientId.Value // O destinatário (usamos .Value por ser Nullable)
			};

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = targetClientIds }
            };

            serverLogicalClock++;
            BroadcastMessageClientRpc(fMessage, serverLogicalClock, clientRpcParams);

            // Não persistimos mensagens privadas
        }
        else // Se não encontramos (targetClientId é nulo)
        {
            // FALHA: Envia uma mensagem de erro APENAS para o remetente
            string fMessage = $"[SISTEMA]: Jogador '{targetNick}' não encontrado.";

            ulong[] targetClientIds = new ulong[] { senderId }; // Apenas o remetente

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = targetClientIds }
            };

            serverLogicalClock++;
            // Usamos o mesmo ClientRpc, mas só para o remetente
            BroadcastMessageClientRpc(fMessage, serverLogicalClock, clientRpcParams);
        }
    }

    /// <summary>
    /// Chamado pelo PlayerData.OnNetworkSpawn() para se registrar no chat.
    /// (SÓ RODA NO SERVIDOR)
    /// </summary>
    //public void RegisterPlayer(ulong netcodeClientId, PlayerData playerData)
    //{
    //    if (!IsServer) return; // Segurança

    //    if (m_PlayerDataMap.ContainsKey(netcodeClientId))
    //    {
    //        Debug.LogWarning($"[ChatManager] Player {netcodeClientId} já está registrado. Sobrescrevendo.");
    //        m_PlayerDataMap[netcodeClientId] = playerData;
    //    }
    //    else
    //    {
    //        m_PlayerDataMap.Add(netcodeClientId, playerData);
    //        Debug.Log($"[ChatManager] Registrou PlayerData para {netcodeClientId}.");
    //    }
    //}

    public void RegisterPlayer(ulong netcodeClientId, PlayerData playerData)
    {
        if (!IsServer) return; // Segurança

        // Pega o nick assim que o PlayerData chega
        string nick = "Jogador (??)";
        if (playerData != null)
        {
            nick = playerData.PlayerNick.Value.ToString();
        }

        if (m_PlayerDataMap.ContainsKey(netcodeClientId))
        {
            Debug.LogWarning($"[ChatManager] Player {netcodeClientId} (Nick: {nick}) já está registrado. Sobrescrevendo.");
            m_PlayerDataMap[netcodeClientId] = playerData;
        }
        else
        {
            m_PlayerDataMap.Add(netcodeClientId, playerData);
            Debug.Log($"[ChatManager] Registrou PlayerData para {netcodeClientId} (Nick: {nick}).");
        }

        // ----> ESTE É O LOCAL CORRETO <----
        // Agora que o jogador está 100% registrado, anuncie sua entrada.
        serverLogicalClock++; // Relógio avança
        BroadcastMessageClientRpc($"[SISTEMA]: {nick} entrou na sala.", serverLogicalClock, default);
    }

    // --- LÓGICA DE CONTROLE CENTRALIZADA ---
    private void Update()
    {
        // --- LÓGICA DE BUSCA MELHORADA ---
        if (localPlayerInputs == null)
        {
            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                localPlayerInputs = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<StarterAssetsInputs>();
            }
        }

        if (localPlayerInputs == null)
        {
            return;
        }

        // Lógica para ABRIR o chat (tecla Enter)
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            if (chatInputField != null && !chatInputField.isFocused && !localPlayerInputs.isChatting)
            {
                EnterChatMode();
            }
        }

        // Lógica para FECHAR o chat (tecla Escape)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (localPlayerInputs.isChatting) // Esta linha agora é segura
            {
                ExitChatMode();
            }
        }
    }

    // Chamado quando o usuário CLICA no InputField
    private void OnSelectChat(string text)
    {
        EnterChatMode();
    }

    // Chamado quando o usuário pressiona ENTER dentro do InputField
    private void OnSubmitChat(string text)
    {
        OnSend();
    }

    // Chamado pelo botão Enviar ou pelo OnSubmitChat
    public void OnSend()
    {
        if (chatInputField == null) return;
        string message = chatInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            // Se a mensagem está vazia, apenas saia do modo chat
            ExitChatMode();
            return;
        }

        if (message.StartsWith("/msg "))
        {
            // Dividimos a mensagem: "/msg" "cugames" "Olá" "tudo" "bem"
            string[] parts = message.Split(' ');

            // Verificação de erro: /msg <nick> <mensagem>
            if (parts.Length < 3)
            {
                AddLocalSystemMessage("[SISTEMA]: Formato incorreto. Use: /msg <Nick_do_Jogador> <mensagem>");
            }
            else
            {
                // 1. O NICK é a segunda parte.
                string targetNick = parts[1];

                // 2. Remonta a mensagem (parts[2] em diante)
                string privateMessage = string.Join(" ", parts, 2, parts.Length - 2);

                // 3. Chama o RPC Privado com o NICK (string), não o ID (ulong)
                SubmitPrivateMessageServerRpc(targetNick, privateMessage);
            }
        }
        else
        {
            // Não é um comando, chama o RPC de broadcast normal (sem mudança aqui)
            SubmitChatMessageServerRpc(message);
        }

        chatInputField.text = "";
        ExitChatMode();
    }

    /// <summary>
    /// Adiciona uma mensagem de REDE (com timestamp) ao log.
    /// </summary>
    private void AddMessageToLog(string message, int timestamp)
    {
        if (chatLogText != null)
        {
            // formatamos a mensagem com o carimbo de Lamport
            string formattedMessage = $"[T:{timestamp}] {message}\n";
            chatLogText.text += formattedMessage;
        }

        if (localPlayerInputs == null || !localPlayerInputs.isChatting)
        {
            StartChatFade(1.0f); // Mostra o chat
            ResetInactivityTimer(); // Inicia o timer para apagar
        }
    }

    /// <summary>
    /// Adiciona uma mensagem de SISTEMA (local) ao log.
    /// Usado para erros de formatação, etc.
    /// </summary>
    private void AddLocalSystemMessage(string message)
    {
        if (chatLogText != null)
        {
            chatLogText.text += message + "\n";
        }

        if (localPlayerInputs == null || !localPlayerInputs.isChatting)
        {
            StartChatFade(1.0f); // Mostra o chat
            ResetInactivityTimer(); // Inicia o timer para apagar
        }
    }

    private void EnterChatMode()
    {
        if (localPlayerInputs == null) return;
        if (localPlayerInputs.isChatting) return; // Já estamos no chat

        localPlayerInputs.isChatting = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (chatInputField != null)
            chatInputField.ActivateInputField();

        StartChatFade(1.0f); // Fade In
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    private void ExitChatMode()
    {
        if (localPlayerInputs == null) return;
        if (!localPlayerInputs.isChatting) return; // Já estamos fora do chat

        localPlayerInputs.isChatting = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (chatInputField != null)
        {
            // Força o InputField a "desselecionar"
            chatInputField.DeactivateInputField();
            // Para garantir, diz ao EventSystem para não focar em nada
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        ResetInactivityTimer(); // Inicia o fade-out
    }

    // --- Lógica de Fade  ---
    private void ResetInactivityTimer()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(InactivityFadeOut());
    }

    private IEnumerator InactivityFadeOut()
    {
        yield return new WaitForSeconds(inactivityTimeout);
        if (chatInputField != null && chatInputField.isFocused)
        {
            yield break;
        }
        StartChatFade(0.0f);
    }

    private void StartChatFade(float targetAlpha)
    {
        StopCoroutine("FadeChatAlpha");
        StartCoroutine(FadeChatAlpha(targetAlpha));
    }

    private IEnumerator FadeChatAlpha(float targetAlpha)
    {
        float time = 0;
        float startAlpha = (chatCanvasGroup != null) ? chatCanvasGroup.alpha : 0;
        while (time < fadeDuration)
        {
            if (chatCanvasGroup == null) yield break;
            chatCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            time += Time.deltaTime;
            yield return null;
        }
        if (chatCanvasGroup != null)
            chatCanvasGroup.alpha = targetAlpha;
    }

    // --- Persistência: POST /chat/message (nick + message_text) ---
    private async Task PersistPublicMessageAsync(string nick, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(apiBaseUrl) || string.IsNullOrEmpty(apiSecretKey))
                return;

            // Evita gravar mensagens do sistema ou vazias
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(nick))
                return;

            string url = $"{apiBaseUrl}/chat/message";
            var body = new ChatPostBody { nick = nick, message_text = message };
            string json = JsonUtility.ToJson(body);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiSecretKey}");

                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning($"[ChatManager] Falha ao persistir mensagem: {request.responseCode} {request.error}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ChatManager] Erro ao persistir mensagem: {e.Message}");
        }
    }

    // --- Histórico: GET /chat/messages ---
    private async Task FetchAndShowHistoryAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(apiBaseUrl) || string.IsNullOrEmpty(apiSecretKey))
                return;

            string url = $"{apiBaseUrl}/chat/messages";
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiSecretKey}");

                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ChatManager] Falha ao obter histórico: {request.responseCode} {request.error}");
                    return;
                }

                // JsonUtility não desserializa arrays raiz. Envelopamos em um objeto.
                string raw = request.downloadHandler.text;
                string wrapped = $"{{\"items\":{raw}}}";
                var wrapper = JsonUtility.FromJson<ChatMessageListWrapper>(wrapped);

                if (wrapper?.items != null)
                {
                    foreach (var msg in wrapper.items)
                    {
                        string line = $"[{msg.nick}]: {msg.message_text}";
                        AddMessageToLog(line, 0); // timestamp "0" para histórico
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ChatManager] Erro ao carregar histórico: {e.Message}");
        }
    }
}