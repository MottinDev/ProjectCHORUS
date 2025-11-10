using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class AuthBase : MonoBehaviour
{
    [Header("Cena para carregar após o Login")]
    public string gameSceneName = "Playground";

    protected virtual async void Awake()
    {
        try
        {
            await UnityServices.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // eventos de falha, expiração e logout
    protected virtual void Start()
    {
        AuthenticationService.Instance.SignInFailed += (err) => {
            Debug.LogError($"Falha na autenticação: {err.Message}");
        };

        AuthenticationService.Instance.SignedOut += () => {
            Debug.Log("Player deslogado.");
        };

        AuthenticationService.Instance.Expired += () => {
            Debug.Log("Sessão do player expirou.");
        };
    }
}