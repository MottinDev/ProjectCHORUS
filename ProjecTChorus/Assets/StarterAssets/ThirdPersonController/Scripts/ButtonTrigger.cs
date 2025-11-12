using UnityEngine;
using Unity.Netcode;

public class ButtonTrigger : MonoBehaviour
{
    // Defina no Inspetor: "A" para um, "B" para o outro
    [SerializeField] private string buttonID;

    private DoorManager doorManager;

    void Start()
    {
        // Encontra o manager (pode ser otimizado, mas funciona)
        doorManager = Object.FindFirstObjectByType<DoorManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Se o objeto que entrou é o Jogador Local
        if (other.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner)
        {
            Debug.Log($"Jogador local apertou o botão {buttonID}");
            // Envia o RPC para o servidor
            if (doorManager != null)
            {
                doorManager.PressButtonServerRpc(buttonID);
            }
        }
    }
}