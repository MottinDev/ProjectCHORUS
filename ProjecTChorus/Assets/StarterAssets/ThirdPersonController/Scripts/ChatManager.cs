using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem; // <-- Importante

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

    // --- (Removemos a flag 'justExitedChat', ela não é mais necessária) ---

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

    // --- (RPCs e Handlers de Conexão - Sem Mudanças) ---
    public override void OnNetworkDespawn() { if (IsServer) { NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected; NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected; } }
    private void HandleClientConnected(ulong clientId) { BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} entrou na sala."); }
    private void HandleClientDisconnected(ulong clientId) { BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} saiu da sala."); }
    [ServerRpc(RequireOwnership = false)] private void SubmitChatMessageServerRpc(string message, ServerRpcParams rParams = default) { ulong senderId = rParams.Receive.SenderClientId; string fMessage = $"[Jogador {senderId}]: {message}"; BroadcastMessageClientRpc(fMessage); }
    [ClientRpc] private void BroadcastMessageClientRpc(string message) { if (chatLogText != null) { chatLogText.text += message + "\n"; } }

    // --- LÓGICA DE CONTROLE CENTRALIZADA (MODIFICADA) ---

    private void Update()
    {
        // Encontra o jogador local (sem mudanças aqui)
        if (localPlayerInputs == null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayerInputs = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<StarterAssetsInputs>();
        }

        if (localPlayerInputs == null) return; // Se não achou o jogador, não faz nada

        // --- (INÍCIO DA MUDANÇA) ---
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
        // --- (FIM DA MUDANÇA) ---
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

        if (!string.IsNullOrWhiteSpace(message))
        {
            SubmitChatMessageServerRpc(message);
            chatInputField.text = "";
        }

        // SEMPRE sai do modo chat após enviar
        ExitChatMode();
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
            // --- (INÍCIO DA MUDANÇA) ---
            // Força o InputField a "desselecionar"
            chatInputField.DeactivateInputField();
            // Para garantir, diz ao EventSystem para não focar em nada
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            // --- (FIM DA MUDANÇA) ---
        }

        ResetInactivityTimer(); // Inicia o fade-out
    }

    // --- (Lógica de Fade - Sem Mudanças) ---
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