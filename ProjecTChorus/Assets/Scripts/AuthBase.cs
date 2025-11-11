using UnityEngine;
using Unity.Services.Core;
using System.Threading.Tasks;

public class AuthBase : MonoBehaviour
{
    [Header("Cena para carregar após o Login")]
    public string gameSceneName = "Playground";

    private Task initializationTask;

    protected virtual void Awake()
    {
        initializationTask = InitializeServicesAsync();
    }

    public Task GetInitializationTask() 
    {         
        return initializationTask;
    }

    protected async Task InitializeServicesAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Services inicializado com sucesso.");
        }
        catch (ServicesInitializationException e)
        {
            Debug.LogError($"Erro na inicialização do Unity Services: {e.Message}");
            throw; // Re-lança o erro para que o LoginManager saiba que falhou
        }
    }
}