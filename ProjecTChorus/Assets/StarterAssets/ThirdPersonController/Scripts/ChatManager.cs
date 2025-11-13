using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
    }

    // --- RPCs e Handlers de Conexão  ---
    public override void OnNetworkDespawn() { if (IsServer) { NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected; NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected; } }
    private void HandleClientConnected(ulong clientId)
    {
        serverLogicalClock++; // Relógio avança
        BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} entrou na sala.", serverLogicalClock, default);
    }
    private void HandleClientDisconnected(ulong clientId)
    {

        if (m_PlayerDataMap.ContainsKey(clientId))
        {
            m_PlayerDataMap.Remove(clientId);
            Debug.Log($"[ChatManager] Desregistrou PlayerData para {clientId}.");
        }

        serverLogicalClock++; // Relógio avança
        BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} saiu da sala.", serverLogicalClock, default);
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
        BroadcastMessageClientRpc(fMessage, serverLogicalClock, default);
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
            // entry.Value é o PlayerData
            // entry.Key é o ulong (ID)

            // Comparamos o nick do jogador no mapa com o nick que recebemos
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
    public void RegisterPlayer(ulong netcodeClientId, PlayerData playerData)
    {
        if (!IsServer) return; // Segurança

        if (m_PlayerDataMap.ContainsKey(netcodeClientId))
        {
            Debug.LogWarning($"[ChatManager] Player {netcodeClientId} já está registrado. Sobrescrevendo.");
            m_PlayerDataMap[netcodeClientId] = playerData;
        }
        else
        {
            m_PlayerDataMap.Add(netcodeClientId, playerData);
            Debug.Log($"[ChatManager] Registrou PlayerData para {netcodeClientId}.");
        }
    }
    // --- LÓGICA DE CONTROLE CENTRALIZADA ---
    private void Update()
    {
        // --- LÓGICA DE BUSCA MELHORADA ---
        // 1. Se não temos o input local, tentamos buscar.
        // Usamos a checagem de "obj == null" que o Unity entende
        // para o caso do PlayerObject ter sido destruído.
        if (localPlayerInputs == null)
        {
            // Só tentamos buscar se o LocalClient e o PlayerObject existirem
            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                localPlayerInputs = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<StarterAssetsInputs>();
            }
        }

        // 2. Se, após a tentativa, ainda for nulo (ou não foi encontrado),
        // não podemos fazer nada. Saia do Update.
        if (localPlayerInputs == null)
        {
            return;
        }

        // --- SE CHEGAMOS AQUI, localPlayerInputs É VÁLIDO ---

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

   
}