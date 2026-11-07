using System.Collections.Generic;
using UnityEngine;

namespace AuraKitchen
{
    public class AuraKitchenEnvironment : MonoBehaviour
    {
        public static AuraKitchenEnvironment Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // The original Kitchen Chaos scene is already fully modeled and lit.
            // Procedural primitives are intentionally disabled to keep the scene clean and performant.
        }
    }
}
