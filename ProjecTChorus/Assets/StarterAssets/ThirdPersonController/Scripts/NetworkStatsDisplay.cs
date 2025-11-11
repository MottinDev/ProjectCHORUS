using Unity.Netcode;
using UnityEngine;
using TMPro;

// 1. MUDANÇA: Voltamos para MonoBehaviour. É mais simples.
public class NetworkStatsDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text latencyText;

    void Start()
    {
        // Começa desabilitado por padrão
        if (latencyText != null)
            latencyText.gameObject.SetActive(false);
    }

    void Update()
    {
        // 0. Checagem de segurança
        if (latencyText == null) return;

        // 1. O NetworkManager existe e está conectado como cliente?
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient)
        {
            // Se não estamos conectados, não mostre nada.
            if (latencyText.gameObject.activeSelf)
                latencyText.gameObject.SetActive(false);
            return;
        }

        // 2. Se chegamos aqui, estamos conectados.
        // Se formos o Host (RTT é 0 e irrelevante), não mostre.
        if (nm.IsHost)
        {
            if (latencyText.gameObject.activeSelf)
                latencyText.gameObject.SetActive(false);
            return;
        }

        // 3. Se chegamos aqui, somos um CLIENTE PURO (não-Host).
        // Então, garanta que o texto está visível.
        if (!latencyText.gameObject.activeSelf)
            latencyText.gameObject.SetActive(true);

        // 4. Pega o RTT
        ulong localClientId = nm.LocalClientId;
        ulong rttInMilliseconds = nm.NetworkConfig.NetworkTransport.GetCurrentRtt(localClientId);

        // 5. Atualiza o texto
        latencyText.text = $"Latência (RTT): {rttInMilliseconds} ms";
    }
}