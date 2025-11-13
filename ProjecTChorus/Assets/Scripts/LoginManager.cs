using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts; // Precisa disso
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginManager : AuthBase
{
    [Header("UI do Login")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;

    [Header("Botões de Login")]
    [Tooltip("Arraste o botão de login de Email/Senha aqui")]
    public Button emailLoginButton;
    [Tooltip("Arraste o botão de login do Google aqui")]
    public Button googleLoginButton;

    [Header("UI de Feedback")]
    public TextMeshProUGUI feedbackText;
    public Color errorColor = Color.red;
    public Color successColor = Color.green;

    private async void Start()
    {
        if (emailLoginButton) emailLoginButton.interactable = false;
        if (googleLoginButton) googleLoginButton.interactable = false;
        if (feedbackText != null) feedbackText.text = "";

        try
        {
            await base.GetInitializationTask();

            AuthenticationService.Instance.SignedIn += OnSignInSuccess;

            // NOVO: Se inscreve nos eventos do Player Accounts (Google, etc.)
            PlayerAccountService.Instance.SignedIn += HandlePlayerAccountSignedIn;
            PlayerAccountService.Instance.SignInFailed += HandlePlayerAccountSignInFailed;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Falha na inicialização. Botões permanecerão desabilitados.");
            Debug.LogException(ex);
            ShowFeedback("Falha ao conectar com os serviços.", errorColor);
            return;
        }

        Debug.Log("Sistema de autenticação pronto. Botões habilitados.");
        if (emailLoginButton) emailLoginButton.interactable = true;
        if (googleLoginButton) googleLoginButton.interactable = true;
    }

    // Mostra uma mensagem de feedback para o usuário na UI
    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
    }

    private async void HandlePlayerAccountSignedIn()
    {
        try
        {
            Debug.Log("Login no Player Account (Google) OK. Agora logando no Authentication Service...");
            // Agora sim, usamos o Access Token para logar no AuthenticationService

            await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);

            // Se chegou aqui, o 'AuthenticationService.Instance.SignedIn' será disparado,
            // e o seu método 'OnSignInSuccess' (que está no AuthBase ou aqui) será chamado.
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Falha ao logar no Authentication Service com o token do Google: {ex.Message}");
            ShowFeedback("Falha ao autenticar com o Google.", errorColor);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"Falha na requisição (SignInWithUnityAsync): {ex.Message}");
            ShowFeedback("Erro de rede. Tente novamente.", errorColor);
        }
    }

    // lidar com falhas
    private void HandlePlayerAccountSignInFailed(RequestFailedException ex)
    {
        Debug.LogError($"Falha no login com Player Account (Google): {ex.Message}");
        ShowFeedback("Falha no login com Google. Tente novamente.", errorColor);
    }


    private async void OnSignInSuccess()
    {
        Debug.Log($"Login com sucesso! PlayerID: {AuthenticationService.Instance.PlayerId}");

        ShowFeedback("Login com sucesso!", successColor);
        await Task.Delay(1000); 

        SceneManager.LoadScene("Lobby");
    }

    public async void OnLoginPressed()
    {
        ShowFeedback("", Color.white);
        string username = usernameInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Usuário e senha não podem ser vazios.", errorColor);
            return;
        }

        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
        }
        catch (RequestFailedException ex)
        {
            // A maioria dos erros de credenciais retorna código 400
            if (ex.ErrorCode == 0)
            {
                ShowFeedback("Usuário ou senha inválidos.", errorColor);
            }
            else
            {
                ShowFeedback("Erro de conexão. Verifique sua internet.", errorColor);
            }

            Debug.LogError($"Erro de login (código {ex.ErrorCode}): {ex.Message}");
            Debug.LogException(ex);
        }
    }

    // A única responsabilidade deste botão é INICIAR o login.
    public async void OnGoogleLoginPressed()
    {
        ShowFeedback("", Color.white);
        try
        {
            ShowFeedback("Abrindo login pela Unity...", Color.white);
            // Apenas inicia o fluxo do navegador.
            await PlayerAccountService.Instance.StartSignInAsync();

            // NÃO COLOQUE NADA DEPOIS DAQUI.
            // O resto do login será feito pelo evento 'HandlePlayerAccountSignedIn'
        }
        catch (AuthenticationException ex)
        {
            ShowFeedback("Não foi possível iniciar o login com a Unity.", errorColor);
            Debug.LogError($"Erro ao INICIAR login com Google: {ex.Message}");
        }
        catch (RequestFailedException ex)
        {
            ShowFeedback("Erro de rede. Tente novamente.", errorColor);
            Debug.LogError($"Falha na requisição (StartSignInAsync): {ex.Message}");
        }
    }

    public void OnRegisterPressed()
    {
        SceneManager.LoadScene("RegisterScene");
    }

    // NOVO: Limpa os eventos quando o objeto for destruído
    private void OnDestroy()
    {
        if (AuthenticationService.Instance != null)
        {
            AuthenticationService.Instance.SignedIn -= OnSignInSuccess;
        }

        if (PlayerAccountService.Instance != null)
        {
            PlayerAccountService.Instance.SignedIn -= HandlePlayerAccountSignedIn;
            PlayerAccountService.Instance.SignInFailed -= HandlePlayerAccountSignInFailed;
        }
    }

}