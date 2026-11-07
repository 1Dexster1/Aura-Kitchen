using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeliveryManager : MonoBehaviour
{
    public static DeliveryManager Instance { get; private set; }

    public event EventHandler<MenuRecipeSO> OnOrderPlaced;
    public event EventHandler<MenuRecipeSO> OnOrderDelivered;
    public event EventHandler<float> OnCountdownStarted;
    public event EventHandler OnFailedOrderDeliver;

    [SerializeField] private MenuSO menu;
    [SerializeField] private float orderSpawnIntervalMin = 10f;
    [SerializeField] private float orderSpawnIntervalMax = 18f;
    [SerializeField] private int maxOrderCount = 3;

    private List<MenuRecipeSO> orders;
    private readonly object ordersMutex = new();

    private int DynamicMaxOrders
    {
        get
        {
            int served = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
            if (served < 2) return 2;
            if (served < 5) return 3;
            return 4;
        }
    }

    private bool CanSpawnOrder => orders.Count < DynamicMaxOrders && GameManager.Instance.IsPlaying;
    private bool HasOrder => orders.Count >= 1;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Cannot have duplicate instance of DeliveryManager");
        }
        Instance = this;
        orders = new List<MenuRecipeSO>(maxOrderCount);
    }

    private void Start()
    {
        GameManager.Instance.OnGameStateChanged += GameManager_OnGameStateChanged;
        StartCoroutine(SpawnOrder());
    }

    private void GameManager_OnGameStateChanged(object sender, GameManager.GameStateChangedArg e)
    {
        if (e.newState == GameManager.State.GAME_START_COUNTDOWN)
        {
            ClearSpawnedOrder();
        }
    }

    private IEnumerator SpawnOrder()
    {
        yield return new WaitForSeconds(2f);

        while (true)
        {
            if (CanSpawnOrder)
            {
                if (HasOrder)
                {
                    // wait a bit for the next order to spawn
                    float nextOrderInterval = UnityEngine.Random.Range(orderSpawnIntervalMin, orderSpawnIntervalMax);
                    OnCountdownStarted?.Invoke(this, nextOrderInterval);
                    yield return new WaitForSeconds(nextOrderInterval);
                }

                if (!CanSpawnOrder)
                {
                    yield return null;
                    continue;
                }

                lock (ordersMutex)
                {
                    // pick an recipe to order
                    int index = UnityEngine.Random.Range(0, menu.menuRecipes.Count);
                    MenuRecipeSO selectedRecipe = menu.menuRecipes[index];
                    orders.Add(selectedRecipe);
                    Debug.LogFormat("<color=green>Order \"{0}\" was placed</color>", selectedRecipe.recipeName);
                    OnOrderPlaced?.Invoke(this, selectedRecipe);
                }

                yield return new WaitForSeconds(3f);
            }
            else
            {
                yield return new WaitUntil(() => CanSpawnOrder);
            }
        }
    }

    private void ClearSpawnedOrder()
    {
        orders.Clear();
    }

    public bool TryDeliverPlate(PlateKitchenObject plate)
    {
        if (!plate.TryMakeMenuItem(out MenuRecipeSO deliveredRecipe))
        {
            Debug.Log("Plate doesn't follow any recipe");
            OnFailedOrderDeliver?.Invoke(this, EventArgs.Empty);
            return false;
        }

        lock (ordersMutex)
        {
            int orderIndex = FindMatchingDeliverOrder(deliveredRecipe);
            if (orderIndex < 0)
            {
                Debug.Log("No matching order");
                OnFailedOrderDeliver?.Invoke(this, EventArgs.Empty);
                return false;
            }
            orders.RemoveAt(orderIndex);
        }

        Debug.LogFormat("Delivered {0}!", deliveredRecipe.recipeName);
        OnOrderDelivered?.Invoke(this, deliveredRecipe);
        return true;
    }

    private int FindMatchingDeliverOrder(MenuRecipeSO recipe)
    {
        return orders.FindIndex(order => order == recipe);
    }
}
