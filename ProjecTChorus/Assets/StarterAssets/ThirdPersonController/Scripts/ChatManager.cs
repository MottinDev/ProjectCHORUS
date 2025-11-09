using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem; 

public class ChatManager : NetworkBehaviour
{
    [Header("UI Elements")]
    public TMP_Text chatLogText;
    public TMP_InputField chatInputField;
    public Button sendButton;

    [Header("Fade Logic")]
    public CanvasGroup chatCanvasGroup;
    public float inactivityTimeout = 10.0f;
    public float fadeDuration = 0.5f;

    private Coroutine fadeCoroutine;
    private StarterAssetsInputs localPlayerInputs;

   

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
    private void HandleClientConnected(ulong clientId) { BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} entrou na sala."); }
    private void HandleClientDisconnected(ulong clientId) { BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} saiu da sala."); }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitChatMessageServerRpc(string message, ServerRpcParams rParams = default)
    {
        ulong senderId = rParams.Receive.SenderClientId;
        string fMessage = $"[Jogador {senderId}]: {message}";

        // Chamamos o ClientRpc sem parâmetros, o que significa "Broadcast para todos"
        BroadcastMessageClientRpc(fMessage, default);
    }

    [ClientRpc]
    private void BroadcastMessageClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        // Apenas chamamos a função local
        AddMessageToLog(message);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPrivateMessageServerRpc(ulong targetClientId, string message, ServerRpcParams rParams = default)
    {
        ulong senderId = rParams.Receive.SenderClientId;
        string fMessage = $"[PRIVADO de {senderId}]: {message}";

        // 1. Cria a lista de clientes que devem receber a mensagem
        ulong[] targetClientIds = new ulong[] {
            senderId,       // O remetente (para ele ver a própria msg)
            targetClientId  // O destinatário
        };

        // 2. Configura os parâmetros de envio
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = targetClientIds
            }
        };

        // 3. Chama o MESMO ClientRpc, mas desta vez com os parâmetros
        // O Netcode vai filtrar e enviar SÓ para os alvos.
        BroadcastMessageClientRpc(fMessage, clientRpcParams);
    }

    // --- LÓGICA DE CONTROLE CENTRALIZADA ---
    private void Update()
    {
        // Encontra o jogador local 
        if (localPlayerInputs == null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayerInputs = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<StarterAssetsInputs>();
        }

        if (localPlayerInputs == null) return; // Se não achou o jogador, não faz nada

        
        // Lógica para ABRIR o chat (tecla Enter)
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            // Se o chat NÃO está focado E o jogador NÃO está no modo chat...
            if (chatInputField != null && !chatInputField.isFocused && !localPlayerInputs.isChatting)
            {
                // ...então o "Enter" foi para ABRIR o chat.
                EnterChatMode();
            }
        }

        // Lógica para FECHAR o chat (tecla Escape)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Se o jogador ESTÁ no modo chat...
            if (localPlayerInputs.isChatting)
            {
                // ...então "Escape" fecha o chat.
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

        // --- LÓGICA DE PARSING ---
        if (message.StartsWith("/msg "))
        {
            // Dividimos a mensagem: "/msg" "1" "Olá" "tudo" "bem"
            string[] parts = message.Split(' ');

            // Verificação de erro: /msg <id> <mensagem>
            if (parts.Length < 3)
            {
                AddMessageToLog("[SISTEMA]: Formato incorreto. Use: /msg <ID_do_Jogador> <mensagem>");
            }
            // Tenta converter a segunda parte (parts[1]) em um ID
            else if (ulong.TryParse(parts[1], out ulong targetClientId))
            {
                // Remonta a mensagem (parts[2] em diante)
                string privateMessage = string.Join(" ", parts, 2, parts.Length - 2);

                // Chama o novo RPC Privado
                SubmitPrivateMessageServerRpc(targetClientId, privateMessage);
            }
            else
            {
                AddMessageToLog($"[SISTEMA]: ID '{parts[1]}' inválido. Deve ser um número.");
            }
        }
        else
        {
            // Não é um comando, chama o RPC de broadcast normal
            SubmitChatMessageServerRpc(message);
        }
        // --- FIM DA LÓGICA ---

        chatInputField.text = "";
        ExitChatMode();
    }
    /// <summary>
    /// Adiciona uma mensagem ao log local da UI e reseta o timer do fade.
    /// Esta função é chamada localmente por todos os ClientRpcs.
    /// </summary>
    private void AddMessageToLog(string message)
    {
        if (chatLogText != null)
        {
            chatLogText.text += message + "\n";
        }

        // Se estivermos no modo chat, não iniciamos o fade-out
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