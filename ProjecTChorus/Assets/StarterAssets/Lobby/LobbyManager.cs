using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Text;
using System;
#if UNITY_SERVER
using Unity.Services.Authentication.Server;
#endif

public class LobbyManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject panelProfile;
    [SerializeField] private GameObject panelLobbyList;
    [SerializeField] private GameObject panelCreateLobby;
    [SerializeField] private GameObject panelJoinedLobby;
    [SerializeField] private GameObject panelInputPassword;

    [Header("Profile UI")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TextMeshProUGUI profileErrorText;

    [Header("Create Lobby UI")]
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private Toggle isPrivateToggle;
    [SerializeField] private Toggle isDedicatedServerToggle;

    [Header("Lobby List UI")]
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private Transform lobbyListContent;

    [Header("Joined Lobby UI")]
    [SerializeField] private TextMeshProUGUI joinedLobbyNameText;
    [SerializeField] private GameObject playerListContainer;
    [SerializeField] private Button startGameButton;
    [SerializeField] private GameObject playerItemPrefab;

    // --- 2. ADICIONADO: Configuração da API Customizada ---
    [Header("Custom API Config")]
    private string m_ApiSecretKey = "MEU_SEGREDO_SUPER_SEGURO_DO_UNITY_12345";

#if UNITY_SERVER
    private string m_ApiBaseUrl = "http://localhost:5000";
#else
    private string m_ApiBaseUrl = "http://3.222.116.91";
#endif

    private float timeSinceLastRefresh = 0f;
    private float refreshCooldown = 5.0f; // 5 segundos de espera

    // --- 3. ADICIONADO: Classe auxiliar para JSON ---
    [System.Serializable]
    private class NickResponse
    {
        public string nick;
    }

    [System.Serializable]
    private class NickCheckResponse
    {
        public bool exists;
    }

    // Variáveis de estado
    private Lobby joinedLobby;
    private string playerName;


    // --- CORREÇÃO DE COROUTINE ---
    // Removemos a Coroutine e usamos um CancellationToken para o loop async
    private CancellationTokenSource heartbeatCancelToken;

    private string connectionType = "dtls";

    // --- PASSO 1: INICIALIZAÇÃO E AUTENTICAÇÃO ---

    async void Start()
    {
        // 1. Esconde todos os painéis
        panelLobbyList.SetActive(false);
        panelCreateLobby.SetActive(false);
        panelJoinedLobby.SetActive(false);
        panelProfile.SetActive(false);

        if (profileErrorText != null)
        {
            profileErrorText.gameObject.SetActive(false);
        }

        try
        {
            // --- 2. ESCOLHE O MÉTODO DE AUTENTICAÇÃO ---

#if UNITY_SERVER
            // --- FLUXO DE AUTENTICAÇÃO DO SERVIDOR DEDICADO (MÉTODO ANTIGO/COMPATÍVEL) ---

            // 1. Define um profile (ainda é uma boa prática)
            InitializationOptions options = new InitializationOptions();
            options.SetProfile("dedicated-server-profile");

            Debug.Log("Servidor Dedicado: Inicializando serviços...");
            await UnityServices.InitializeAsync(options);

            // 2. Lê as chaves que o docker-compose injetou
            string keyId = Environment.GetEnvironmentVariable("UNITY_SERVICE_KEY_ID");
            string secretKey = Environment.GetEnvironmentVariable("UNITY_SERVICE_SECRET_KEY");

            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secretKey))
            {
                Debug.LogError("FALHA CRÍTICA: Chaves de Serviço (Key ID ou Secret Key) não encontradas. Verifique o .env e docker-compose.yml.");
                return; // Não continuar
            }

            // 3. Autentica usando o AuthenticationService diretamente
            Debug.Log($"Serviços inicializados. Autenticando com Key ID: {keyId}...");

            await ServerAuthenticationService.Instance.SignInWithServiceAccountAsync(keyId, secretKey);

            Debug.Log($"Servidor autenticado com sucesso via Key ID: {keyId}");

            // O atraso de 2s ainda é uma boa ideia
            await Task.Delay(2000);

            Debug.Log("Sincronização concluída. Criando lobby...");
            AutoCreateLobby_Server();

#else
            // --- FLUXO DE AUTENTICAÇÃO DO CLIENTE (JOGADOR) ---
            string profile = "default-profile";
#if UNITY_EDITOR
                profile = "editor-profile-" + UnityEngine.Random.Range(1, 1000);
#endif

            InitializationOptions options = new InitializationOptions();
            options.SetProfile(profile);
            
            await UnityServices.InitializeAsync(options);

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            Debug.Log($"Cliente autenticado: {AuthenticationService.Instance.PlayerId}");

            Debug.Log("Este é um Client. Checando perfil...");
            await CheckProfileAsync();
            desbloquearCursor();
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao inicializar ou autenticar: {e}");
        }
    }

    /// <summary>
    /// Esta função é chamada AUTOMATICAMENTE se o build for UNITY_SERVER.
    /// Ela cria um lobby público e inicia o servidor.
    /// </summary>
    private async void AutoCreateLobby_Server()
    {
        // Defina os valores do seu servidor aqui
        string lobbyName = "Servidor Dedicado (ProjectCHORUS)";
        bool isPrivate = false;
        int maxPlayers = 4; // Ou o seu máximo

        try
        {
            // 1. O Player é 'null' porque somos um servidor dedicado
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = null
            };

            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            // 2. Aloca o Relay (idêntico)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[Servidor] Lobby Criado! Join Code: {joinCode}");

            // 3. Salva o Join Code no Lobby (idêntico)
            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                { { "RELAY_JOIN_CODE", new DataObject(DataObject.VisibilityOptions.Member, joinCode) } }
            });

            await Task.Delay(300);

            // 4. Configura o Transporte (idêntico)
            UnityTransport utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // 5. Inicia o NetworkManager como SERVIDOR (não Host)
            NetworkManager.Singleton.StartServer();

            Debug.Log($"[Servidor] Servidor iniciado e lobby '{lobbyName}' está público.");

            // 6. Inicia o Heartbeat (Obrigatório!)
            // O servidor também precisa manter o lobby vivo.
            StartHeartbeat(15f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Servidor] FALHA ao criar lobby: {e}");
        }
    }

    private async Task CheckProfileAsync()
    {
        string authId = AuthenticationService.Instance.PlayerId;
        string url = $"{m_ApiBaseUrl}/player/{authId}/nick";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {m_ApiSecretKey}");

            try
            {
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success) // Código 200
                {
                    // --- NICK ENCONTRADO ---
                    string jsonResponse = request.downloadHandler.text;
                    NickResponse data = JsonUtility.FromJson<NickResponse>(jsonResponse);
                    playerName = data.nick; // Salva o nick globalmente

                    PlayerNickBridge.NickToCarry = playerName;

                    Debug.Log($"[Profile] Nick '{playerName}' carregado da API.");

                    // Sincroniza o nick com o Unity Auth (boa prática)
                    await UpdatePlayerNameAsync(playerName);

                    // Redireciona para o Jogo (Lobby List)
                    panelProfile.SetActive(false);
                    panelLobbyList.SetActive(true);
                    RefreshLobbyList(); // Já carrega a lista
                }
                else if (request.responseCode == 404)
                {
                    // --- NICK NÃO ENCONTRADO ---
                    Debug.Log("[Profile] Nick não encontrado. Exibindo painel de criação.");

                    // Mostra o painel de criação
                    panelProfile.SetActive(true);
                    panelLobbyList.SetActive(false);
                }
                else
                {
                    // Outro erro
                    throw new System.Exception($"Erro da API: {request.error}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Profile] Falha ao checar nick: {e.Message}");
                // Fallback: Se a API falhar, mostrar o painel de profile
                panelProfile.SetActive(true);
                panelLobbyList.SetActive(false);
            }
        }
    }

    //public async void OnConfirmProfile()
    //{
    //    playerName = playerNameInput.text;
    //    if (string.IsNullOrWhiteSpace(playerName))
    //    {
    //        Debug.LogWarning("O nome não pode estar vazio.");
    //        return;
    //    }

    //    try
    //    {
    //        bool nickAlreadyExists = await CheckNickExistsAsync(playerName);
    //        if (nickAlreadyExists)
    //        {
    //            Debug.LogWarning($"[Profile] O nick '{playerName}' já está em uso. Tente outro.");
    //            // TODO: Mostrar esta mensagem de erro para o usuário na UI
    //            return; // Para a execução
    //        }
    //    }
    //    catch (System.Exception e)
    //    {
    //        Debug.LogError($"[Profile] Falha ao VERIFICAR o nick na API: {e.Message}");
    //        // Não continuar se o sistema de verificação falhar
    //        return;
    //    }

    //    PlayerNickBridge.NickToCarry = playerName;
    //    // --- 1. Salvar na API Customizada (POST) ---
    //    string authId = AuthenticationService.Instance.PlayerId;
    //    string url = $"{m_ApiBaseUrl}/player/{authId}/nick";

    //    string jsonPayload = $"{{\"nick\":\"{playerName}\"}}";
    //    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

    //    using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
    //    {
    //        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
    //        request.downloadHandler = new DownloadHandlerBuffer();
    //        request.SetRequestHeader("Content-Type", "application/json");
    //        request.SetRequestHeader("Authorization", $"Bearer {m_ApiSecretKey}");

    //        try
    //        {
    //            await request.SendWebRequest();
    //            if (request.result != UnityWebRequest.Result.Success)
    //            {
    //                throw new System.Exception(request.error);
    //            }
    //            Debug.Log($"[Profile] Nick '{playerName}' salvo na API customizada.");
    //        }
    //        catch (System.Exception e)
    //        {
    //            Debug.LogError($"[Profile] Falha ao SALVAR nick na API: {e.Message}");
    //            // Não continuar se não conseguir salvar
    //            return;
    //        }
    //    }

    //    // --- 2. Salvar no Unity Auth (código original) ---
    //    await UpdatePlayerNameAsync(playerName);

    //    // --- 3. Mudar de painel (código original) ---
    //    panelProfile.SetActive(false);
    //    panelLobbyList.SetActive(true);
    //    RefreshLobbyList();
    //}

    public async void OnConfirmProfile()
    {
        // --- ATUALIZADO: Limpa erros antigos ---
        if (profileErrorText != null)
        {
            profileErrorText.text = "";
            profileErrorText.gameObject.SetActive(false);
        }

        playerName = playerNameInput.text;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            Debug.LogWarning("O nome não pode estar vazio.");
            ShowProfileError("O nome não pode estar vazio."); // <-- MOSTRA O ERRO
            return;
        }

        // --- 1. VERIFICAR SE O NICK JÁ EXISTE ---
        try
        {
            bool nickAlreadyExists = await CheckNickExistsAsync(playerName);
            if (nickAlreadyExists)
            {
                Debug.LogWarning($"[Profile] O nick '{playerName}' já está em uso. Tente outro.");
                ShowProfileError($"O nick '{playerName}' já está em uso."); // <-- MOSTRA O ERRO
                return; // Para a execução
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Profile] Falha ao VERIFICAR o nick na API: {e.Message}");
            ShowProfileError("Erro ao conectar com o servidor. Tente mais tarde."); // <-- MOSTRA O ERRO
            return;
        }

        // Se chegou aqui, o nick está livre
        PlayerNickBridge.NickToCarry = playerName;

        // --- 2. Salvar na API Customizada (POST) ---
        string authId = AuthenticationService.Instance.PlayerId;
        string url = $"{m_ApiBaseUrl}/player/{authId}/nick";

        string jsonPayload = $"{{\"nick\":\"{playerName}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {m_ApiSecretKey}");

            try
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new System.Exception(request.error);
                }
                Debug.Log($"[Profile] Nick '{playerName}' salvo na API customizada.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Profile] Falha ao SALVAR nick na API: {e.Message}");
                ShowProfileError("Erro ao salvar o nick. Tente mais tarde."); // <-- MOSTRA O ERRO
                return;
            }
        }

        // --- 3. Salvar no Unity Auth (código original) ---
        await UpdatePlayerNameAsync(playerName);

        // --- 4. Mudar de painel (código original) ---
        panelProfile.SetActive(false);
        panelLobbyList.SetActive(true);
        RefreshLobbyList();
    }

    /// <summary>
    /// Chama a API customizada para verificar se um nick já existe.
    /// </summary>
    /// <returns>True se o nick existir, False se estiver livre.</returns>
    private async Task<bool> CheckNickExistsAsync(string nickName)
    {
        // Codifica o nick para ser seguro em uma URL (ex: "Nick Teste" vira "Nick%20Teste")
        string encodedNick = UnityWebRequest.EscapeURL(nickName);
        string url = $"{m_ApiBaseUrl}/nicks/check?name={encodedNick}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {m_ApiSecretKey}");

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                NickCheckResponse data = JsonUtility.FromJson<NickCheckResponse>(jsonResponse);
                return data.exists; // Retorna true (se existe) ou false (se não existe)
            }
            else
            {
                // Se a API der erro, lança uma exceção que será pega pelo OnConfirmProfile
                throw new System.Exception($"Erro da API ao checar nick: {request.error}");
            }
        }
    }

    public void desbloquearCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void showCreateLobby()
    {
        panelLobbyList.SetActive(false);
        panelCreateLobby.SetActive(true);
    }

    public void showLobbyList()
    {
        panelCreateLobby.SetActive(false);
        panelLobbyList.SetActive(true);
    }

    public void hideProfilePanel()
    {
        panelProfile.SetActive(false);
    }

    public void hideLobbyList()
    {
        panelCreateLobby.SetActive(false);
        panelProfile.SetActive(true);

    }

    private void ShowProfileError(string message)
    {
        if (profileErrorText == null) return;

        profileErrorText.text = message;
        profileErrorText.gameObject.SetActive(true);
    }

    //public async void OnConfirmProfile()
    //{
    //    playerName = playerNameInput.text;
    //    await UpdatePlayerNameAsync(playerName);

    //    panelProfile.SetActive(false);
    //    panelLobbyList.SetActive(true);

    //    RefreshLobbyList();
    //}

    private async Task UpdatePlayerNameAsync(string name)
    {
        try
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
            Debug.Log($"Player name updated to: {name}");
        }
        catch (AuthenticationException e)
        {
            Debug.LogError($"Failed to update player name: {e}");
        }
    }

    // --- PASSO 2: CRIAR E LISTAR LOBBIES ---

    public async void RefreshLobbyList()
    {
        // Checa se já passou tempo suficiente desde a última atualização
        if (Time.time < timeSinceLastRefresh + refreshCooldown)
        {
            Debug.LogWarning("RefreshLobbyList chamado muito rápido. Esperando cooldown.");
            return; // Sai da função mais cedo
        }

        // Atualiza o tempo da última atualização
        timeSinceLastRefresh = Time.time;

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

            Debug.Log($"Found {response.Results.Count} lobbies.");

            foreach (Transform child in lobbyListContent)
            {
                Destroy(child.gameObject);
            }

            foreach (Lobby lobby in response.Results)
            {
                Lobby lobbyAtual = lobby;

                GameObject lobbyItem = Instantiate(lobbyItemPrefab, lobbyListContent);
                lobbyItem.GetComponentInChildren<TextMeshProUGUI>().text = lobbyAtual.Name;
                lobbyItem.GetComponent<Button>().onClick.AddListener(() =>
                {
                    OnJoinLobby(lobbyAtual);
                });
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to query lobbies: {e}");
        }
    }

    public async void OnCreateLobby()
    {
        string lobbyName = lobbyNameInput.text;
        bool isPrivate = isPrivateToggle.isOn;
        int maxPlayers = 4;

        try
        {
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = isDedicatedServerToggle.isOn ? null : GetPlayer()
            };
            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"Lobby Created! Join Code: {joinCode}");

            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RELAY_JOIN_CODE", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            });

            await Task.Delay(300);

            UnityTransport utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));
            if (isDedicatedServerToggle.isOn)
            {
                NetworkManager.Singleton.StartServer();
            }
            else
            {
                NetworkManager.Singleton.StartHost();
            }

            panelCreateLobby.SetActive(false);
            panelJoinedLobby.SetActive(true);

            // --- CORREÇÃO DE COROUTINE ---
            // Inicia o novo loop async
            StartHeartbeat(15f);

            UpdateJoinedLobbyUI(joinedLobby);

            desbloquearCursor();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to create lobby: {e}");
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to create relay: {e}");
        }
    }

    // --- PASSO 3: ENTRAR EM UM LOBBY ---

    public async void OnJoinLobby(Lobby lobbyToJoin)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions { Player = GetPlayer() };
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyToJoin.Id, options);

            Debug.Log($"Entrou no lobby '{joinedLobby.Name}' com sucesso. Verificando dados do Relay...");

            if (!joinedLobby.Data.ContainsKey("RELAY_JOIN_CODE"))
            {
                Debug.LogError("FALHA: O Host ainda não salvou o código do Relay. (Race Condition)");
                return;
            }
            string joinCode = joinedLobby.Data["RELAY_JOIN_CODE"].Value;
            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("FALHA: O código do Relay foi encontrado, mas estava vazio.");
                return;
            }

            Debug.Log($"Código do Relay encontrado: {joinCode}. Tentando entrar...");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Debug.Log("Conectado ao Relay com sucesso.");

            UnityTransport utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, connectionType));
            NetworkManager.Singleton.StartClient();

            panelLobbyList.SetActive(false);
            panelJoinedLobby.SetActive(true);

            // --- CORREÇÃO DE COROUTINE ---
            // Inicia o novo loop async
            StartHeartbeat(15f);

            UpdateJoinedLobbyUI(joinedLobby);

            desbloquearCursor();

        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to join lobby: {e}");
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to join relay: {e}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro inesperado ao entrar no lobby: {e}");
        }
    }

    private void UpdateJoinedLobbyUI(Lobby lobby)
    {
        if (lobby == null || playerListContainer == null) return;

        joinedLobbyNameText.text = lobby.Name;

        foreach (Transform child in playerListContainer.transform)
        {
            if (Application.isPlaying)
            {
                // Se sim, usa o Destroy normal.
                Destroy(child.gameObject);
            }
            else
            {
                // Se não (o async atrasou), usa o DestroyImmediate
                // para obedecer o Unity Editor.
                DestroyImmediate(child.gameObject);
            }
        }

        foreach (Player player in lobby.Players)
        {
            GameObject playerItem = Instantiate(playerItemPrefab, playerListContainer.transform);

            string playerName = player.Data["PlayerName"].Value;

            if (player.Id == lobby.HostId)
            {
                playerItem.GetComponentInChildren<TextMeshProUGUI>().text = $"{playerName} (Host)";
            }
            else
            {
                playerItem.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
            }
        }

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(AuthenticationService.Instance.PlayerId == lobby.HostId);
        }
    }

    // --- NOVO: FUNÇÕES DE AJUDA PARA O LOOP DE HEARTBEAT ---

    private void StartHeartbeat(float waitTimeSeconds)
    {
        StopHeartbeat(); // Para o loop anterior, se houver
        heartbeatCancelToken = new CancellationTokenSource();
        // Inicia o loop "fire-and-forget" (sem 'await')
        LobbyHeartbeatLoop(waitTimeSeconds, heartbeatCancelToken.Token);
    }

    private void StopHeartbeat()
    {
        if (heartbeatCancelToken != null)
        {
            heartbeatCancelToken.Cancel();
            heartbeatCancelToken = null;
        }
    }

    private async void LobbyHeartbeatLoop(float waitTimeSeconds, CancellationToken token)
    {
        // O loop para se o token for cancelado (pelo StopHeartbeat) 
        // ou se sairmos do lobby (joinedLobby = null)
        while (joinedLobby != null && !token.IsCancellationRequested)
        {
            bool isHost = AuthenticationService.Instance.PlayerId == joinedLobby.HostId;

            try
            {
                // Os 'await' agora são válidos porque o método é 'async'
                if (isHost)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
                }

                joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

#if !UNITY_SERVER
                    UpdateJoinedLobbyUI(joinedLobby);
#endif
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"Lobby Heartbeat failed: {e.Message}");
                if (e.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    panelJoinedLobby.SetActive(false);
                    panelLobbyList.SetActive(true);
                    joinedLobby = null; // Isso fará o loop parar
                }
            }
            catch (System.Exception e)
            {
                // Se o token for cancelado, o Task.Delay lança uma exceção
                if (e is TaskCanceledException) break;
                Debug.LogError($"Erro no Heartbeat: {e}");
            }

            // 'await' agora é válido. Também passamos o token para que ele
            // pare imediatamente se for cancelado.
            try
            {
                await Task.Delay((int)(waitTimeSeconds * 1000), token);
            }
            catch (TaskCanceledException)
            {
                // Esperado quando o StopHeartbeat é chamado
                break;
            }
        }
    }

    // Helper (função de ajuda)
    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
            }
        };
    }

    // --- PASSO 4: MUDAR DE CENA ---
    public void OnStartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        StartCoroutine(DelayedSceneLoad());
    }

    private IEnumerator DelayedSceneLoad()
    {
        yield return new WaitForSeconds(0.2f); // tempo para o player ser spawnado
        NetworkManager.Singleton.SceneManager.LoadScene("Playground", LoadSceneMode.Single);
    }

}