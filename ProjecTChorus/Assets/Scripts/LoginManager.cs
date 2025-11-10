using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginManager : AuthBase
{
    [Header("UI do Login")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;

    protected override void Start()
    {
        base.Start(); // Chama o Start() do AuthBase (que cuida das falhas)

        AuthenticationService.Instance.SignedIn += OnSignInSuccess;
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
            // Se der certo, o evento "OnSignInSuccess" será disparado
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

    public void OnRegisterPressed()
    {
        SceneManager.LoadScene("RegisterScene");
    }
}