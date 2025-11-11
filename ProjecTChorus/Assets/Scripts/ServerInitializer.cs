using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class ServerInitializer : MonoBehaviour
{
    async void Start()
    {
        await InitializeServerAsync();
    }

    [Obsolete]
    private async Task InitializeServerAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();

            // Login como "usuário do servidor"
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"✅ Servidor autenticado como usuário técnico: {AuthenticationService.Instance.PlayerId}");
            }

            // Exemplo: salvar estado da partida
            await CloudSaveService.Instance.Data.ForceSaveAsync(new Dictionary<string, object>
            {
                { "server_status", "online" },
                { "timestamp", DateTime.UtcNow.ToString("o") }
            });

            Debug.Log("✅ Cloud Save funcionando no servidor headless!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Falha ao inicializar servidor: {ex.Message}");
        }
    }
}
