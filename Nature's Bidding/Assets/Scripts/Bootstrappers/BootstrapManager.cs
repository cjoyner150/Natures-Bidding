using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class BootstrapManager : MonoBehaviour
{
    async void Start()
    {
        await NetworkSessionManager.Instance.WaitForAuth();
        await PersistentGameStateManager.Instance.LoadMenuScene();
    }
}
