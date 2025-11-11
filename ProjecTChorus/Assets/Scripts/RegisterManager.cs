using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RegisterManager : AuthBase
{
    [Header("UI do Cadastro")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    /* Chama o Start() do AuthBase, que não escuta o evento SignedIn */

    public async void OnRegisterPressed()
    {
        string username = usernameInput.text;
        string password = passwordInput.text;
        string confirmPassword = confirmPasswordInput.text;

        // Validação de senha
        if (password != confirmPassword)
        {
            Debug.LogError("As senhas não conferem!");
            return;
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Usuário e senha não podem ser vazios.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            Debug.Log($"Cadastro bem-sucedido para o usuário: {username}");

            AuthenticationService.Instance.SignOut();

            Debug.Log("Cadastro completo! Redirecionando para a tela de Login.");
            SceneManager.LoadScene("LoginScene");
        }
        catch (AuthenticationException ex)
        {
            if (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                Debug.LogError("Este nome de usuário já existe.");
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

    public void OnBackToLoginPressed()
    {
        SceneManager.LoadScene("LoginScene");
    }
}