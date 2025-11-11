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

    private async void Start()
    {
        if (emailLoginButton) emailLoginButton.interactable = false;
        if (googleLoginButton) googleLoginButton.interactable = false;

        try
        {
            await base.GetInitializationTask();

            // NOVO: Se inscreve nos eventos do Player Accounts (Google, etc.)
            PlayerAccountService.Instance.SignedIn += HandlePlayerAccountSignedIn;
            PlayerAccountService.Instance.SignInFailed += HandlePlayerAccountSignInFailed;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Falha na inicialização. Botões permanecerão desabilitados.");
            Debug.LogException(ex); 
            return;
        }

        Debug.Log("Sistema de autenticação pronto. Botões habilitados.");
        if (emailLoginButton) emailLoginButton.interactable = true;
        if (googleLoginButton) googleLoginButton.interactable = true;
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
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"Falha na requisição (SignInWithUnityAsync): {ex.Message}");
        }
    }

    // lidar com falhas
    private void HandlePlayerAccountSignInFailed(RequestFailedException ex)
    {
        Debug.LogError($"Falha no login com Player Account (Google): {ex.Message}");
    }


    private void OnSignInSuccess()
    {
        Debug.Log($"Login com sucesso! PlayerID: {AuthenticationService.Instance.PlayerId}");
        SceneManager.LoadScene(gameSceneName);
    }

    public async void OnLoginPressed()
    {
        string username = usernameInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Usuário e senha não podem ser vazios.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            // Se der certo, o evento 'AuthenticationService.Instance.SignedIn'
            // dispara e seu 'OnSignInSuccess' (que você já tem) cuida da troca de cena.
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError("Usuário ou senha inválidos.");
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }

    // A única responsabilidade deste botão é INICIAR o login.
    public async void OnGoogleLoginPressed()
    {
        try
        {
            // Apenas inicia o fluxo do navegador.
            await PlayerAccountService.Instance.StartSignInAsync();

            // NÃO COLOQUE NADA DEPOIS DAQUI.
            // O resto do login será feito pelo evento 'HandlePlayerAccountSignedIn'
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Erro ao INICIAR login com Google: {ex.Message}");
        }
        catch (RequestFailedException ex)
        {
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
        if (PlayerAccountService.Instance != null)
        {
            PlayerAccountService.Instance.SignedIn -= HandlePlayerAccountSignedIn;
            PlayerAccountService.Instance.SignInFailed -= HandlePlayerAccountSignInFailed;
        }
    }
}