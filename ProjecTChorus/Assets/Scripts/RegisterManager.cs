using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks; // Necessário para o Delay

public class RegisterManager : AuthBase
{
    [Header("UI do Cadastro")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    [Header("UI de Feedback")]
    public TextMeshProUGUI feedbackText; // <<< NOVO: Arraste seu texto de UI aqui
    public Color errorColor = Color.red;
    public Color successColor = Color.green;

    void Start()
    {
        // Limpa a mensagem de feedback ao iniciar
        if (feedbackText != null)
            feedbackText.text = "";
    }

    /// <summary>
    /// Mostra uma mensagem de feedback para o usuário na UI
    /// </summary>
    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
    }

    public async void OnRegisterPressed()
    {
        // Limpa o feedback anterior
        ShowFeedback("", Color.white);

        string username = usernameInput.text;
        string password = passwordInput.text;
        string confirmPassword = confirmPasswordInput.text;

        // --- Validação ---

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Usuário e senha não podem ser vazios.", errorColor);
            return;
        }

        // Validação das 5 regras de senha (do PasswordValidator)
        var validation = PasswordValidator.ValidatePassword(password);

        // Verifica cada regra e dá um feedback específico
        if (!validation.HasMinLength) { ShowFeedback("A senha deve ter no mínimo 8 caracteres.", errorColor); return; }
        if (!validation.HasUppercase) { ShowFeedback("A senha deve ter 1 letra maiúscula.", errorColor); return; }
        if (!validation.HasLowercase) { ShowFeedback("A senha deve ter 1 letra minúscula.", errorColor); return; }
        if (!validation.HasNumber) { ShowFeedback("A senha deve ter 1 número.", errorColor); return; }
        if (!validation.HasSpecialChar) { ShowFeedback("A senha deve ter 1 caractere especial.", errorColor); return; }

        // Validação de confirmação
        if (password != confirmPassword)
        {
            ShowFeedback("As senhas não conferem!", errorColor);
            return;
        }

        // --- Fim da Validação ---

        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);

            Debug.Log($"Cadastro bem-sucedido para o usuário: {username}");

            // Feedback de sucesso!
            ShowFeedback("Cadastro bem-sucedido!", successColor);

            // Espera 2 segundos para o usuário ler a mensagem
            await Task.Delay(2000);

            AuthenticationService.Instance.SignOut();
            Debug.Log("Cadastro completo! Redirecionando para a tela de Login.");
            SceneManager.LoadScene("LoginScene");
        }
        catch (AuthenticationException ex)
        {
            if (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked) // Adicionei este
            {
                ShowFeedback("Este nome de usuário já existe.", errorColor);
            }
            else
            {
                ShowFeedback($"Erro de autenticação: {ex.Message}", errorColor);
                Debug.LogException(ex);
            }
        }
        catch (RequestFailedException ex)
        {
            ShowFeedback($"Erro de rede. Tente novamente.", errorColor);
            Debug.LogException(ex);
        }
    }

    public void OnBackToLoginPressed()
    {
        SceneManager.LoadScene("LoginScene");
    }
}