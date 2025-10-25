using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI; 
using Unity.Services.Core;
using Unity.Services.Authentication;

public class AuthManager : MonoBehaviour
{
    public InputField usernameInput;
    public InputField passwordInput;

    async void Awake()
    {
        try
        {
            await UnityServices.InitializeAsync();
            SetupEvents(); 
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    void SetupEvents()
    {
        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");
            Debug.Log($"AccessToken: {AuthenticationService.Instance.AccessToken}");
            Debug.Log("Login bem-sucedido!");

            // UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        };

        AuthenticationService.Instance.SignInFailed += (err) =>
        {
            Debug.LogError($"Falha no login: {err.Message}");
        };

        AuthenticationService.Instance.SignedOut += () =>
        {
            Debug.Log("Player deslogado.");
        };

        AuthenticationService.Instance.Expired += () =>
        {
            Debug.Log("Sessão do player expirou.");
        };
    }
    public async void SignUp()
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
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            Debug.Log($"Cadastro bem-sucedido para o usuário: {username}");
        }
        catch (AuthenticationException ex)
        {
            if (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                Debug.LogError("Usuário já existe.");
            }
            else
            {
                Debug.LogException(ex);
            }
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }

    public async void SignIn()
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
}