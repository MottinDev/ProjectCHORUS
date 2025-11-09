using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem; // <-- IMPORTANTE: Adicione isso

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
    private StarterAssetsInputs localPlayerInputs; // Referência ao nosso jogador

    void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSend);
        }
        if (chatInputField != null)
        {
            // OUVINTES DE EVENTOS (A CORREÇÃO PRINCIPAL)
            chatInputField.onSubmit.AddListener(OnSubmitChat);
            chatInputField.onSelect.AddListener(OnSelectChat);
            chatInputField.onDeselect.AddListener(OnDeselectChat);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        // Inicia o chat invisível
        if (chatCanvasGroup != null)
        {
            chatCanvasGroup.alpha = 0f;
        }
    }

    // --- (O resto do OnNetworkDespawn, Handlers, RPCs não muda) ---
    // ... (copie e cole os seus métodos HandleClientConnected, HandleClientDisconnected, SubmitChatMessageServerRpc, BroadcastMessageClientRpc aqui) ...

    public override void OnNetworkDespawn() { if (IsServer) { NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected; NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected; } }
    private void HandleClientConnected(ulong clientId) { BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} entrou na sala."); }
    private void HandleClientDisconnected(ulong clientId) { BroadcastMessageClientRpc($"[SISTEMA]: Jogador {clientId} saiu da sala."); }
    [ServerRpc(RequireOwnership = false)] private void SubmitChatMessageServerRpc(string message, ServerRpcParams rpcParams = default) { ulong senderId = rpcParams.Receive.SenderClientId; string formattedMessage = $"[Jogador {senderId}]: {message}"; BroadcastMessageClientRpc(formattedMessage); }
    [ClientRpc] private void BroadcastMessageClientRpc(string message) { if (chatLogText != null) { chatLogText.text += message + "\n"; } }

    // --- LÓGICA DE CONTROLE CENTRALIZADA ---

    private void Update()
    {
        // Se o jogador não for encontrado, tenta encontrá-lo
        if (localPlayerInputs == null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayerInputs = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<StarterAssetsInputs>();
        }

        // Se o jogador local existir...
        if (localPlayerInputs != null)
        {
            // O 'Update' agora detecta "Enter" para ABRIR o chat
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                // Só abre se não estiver no chat
                if (!localPlayerInputs.isChatting)
                {
                    EnterChatMode();
                }
            }
        }
    }

    // Chamado quando o usuário CLICA no InputField
    private void OnSelectChat(string text)
    {
        EnterChatMode();
    }

    // Chamado quando o usuário CLICA FORA do InputField
    private void OnDeselectChat(string text)
    {
        // Só sai do modo chat se não estivermos enviando uma mensagem
        // (o 'onSubmit' cuida disso)
        if (localPlayerInputs != null && localPlayerInputs.isChatting)
        {
            ExitChatMode();
        }
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

    // Função única para ENTRAR no modo chat
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

    // Função única para SAIR do modo chat
    private void ExitChatMode()
    {
        if (localPlayerInputs == null) return;
        if (!localPlayerInputs.isChatting) return; // Já estamos fora do chat

        localPlayerInputs.isChatting = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (chatInputField != null)
            chatInputField.DeactivateInputField();

        ResetInactivityTimer(); // Inicia o fade-out
    }

    // --- (Nenhuma mudança na lógica de FADE) ---

    private void ResetInactivityTimer()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(InactivityFadeOut());
    }

    private IEnumerator InactivityFadeOut()
    {
        yield return new WaitForSeconds(inactivityTimeout);

        // Se o usuário re-selecionou o chat, não faz o fade
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