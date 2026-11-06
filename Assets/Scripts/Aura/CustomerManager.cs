using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AuraKitchen
{
    public class CustomerManager : MonoBehaviour
    {
        public static CustomerManager Instance { get; private set; }

        public event EventHandler<CustomerServedStatsEventArgs> OnCustomerStatsUpdated;
        public event EventHandler<int> OnTipsUpdated;
        public event EventHandler<Customer> OnCustomerArrived;
        public event EventHandler<Customer> OnCustomerDeparted;

        public class CustomerServedStatsEventArgs : EventArgs
        {
            public int HappyCustomers;
            public int MissedCustomers;
            public int TotalTips;
            public float AverageRating;
        }

        [Header("Prefabs & References")]
        [SerializeField] private GameObject customerPrefab;
        [SerializeField] private Transform customerSpawnPoint;
        [SerializeField] private Transform customerExitPoint;
        [SerializeField] private Transform[] diningSeats;

        [Header("Spawn Settings")]
        [SerializeField] private float minSpawnDelay = 4f;
        [SerializeField] private float maxSpawnDelay = 9f;
        [SerializeField] private int maxSimultaneousCustomers = 4;

        private List<Customer> activeCustomers = new List<Customer>();
        private bool[] seatOccupied;
        private int totalTipsEarned = 0;
        private int happyCustomersCount = 0;
        private int missedCustomersCount = 0;
        private float accumulatedRating = 0f;

        public int TotalTips => totalTipsEarned;
        public int HappyCount => happyCustomersCount;
        public int MissedCount => missedCustomersCount;
        public float AverageStarRating => happyCustomersCount > 0 ? (accumulatedRating / happyCustomersCount) : 5.0f;
        public IReadOnlyList<Customer> ActiveCustomers => activeCustomers;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureDefaultSeatPositions();
            seatOccupied = new bool[diningSeats.Length];
        }

        private void Start()
        {
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderDelivered += DeliveryManager_OnOrderDelivered;
            }

            StartCoroutine(CustomerSpawnLoop());
        }

        private void OnDestroy()
        {
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderDelivered -= DeliveryManager_OnOrderDelivered;
            }
        }

        private void EnsureDefaultSeatPositions()
        {
            // If diningSeats are not assigned in Inspector, create procedural dining counter seats
            if (diningSeats == null || diningSeats.Length == 0)
            {
                diningSeats = new Transform[maxSimultaneousCustomers];
                GameObject diningParent = new GameObject("Aura_DiningSeats");
                diningParent.transform.SetParent(transform);

                // Default positions in front of the kitchen service counter
                for (int i = 0; i < maxSimultaneousCustomers; i++)
                {
                    GameObject seat = new GameObject($"Seat_{i + 1}");
                    seat.transform.SetParent(diningParent.transform);
                    // Place along the front of the restaurant (Z = -5, spaced along X)
                    float posX = -3.5f + (i * 2.3f);
                    seat.transform.position = new Vector3(posX, 0f, -5.2f);
                    diningSeats[i] = seat.transform;
                }
            }

            if (customerSpawnPoint == null)
            {
                GameObject spawn = new GameObject("Aura_CustomerSpawnPoint");
                spawn.transform.SetParent(transform);
                spawn.transform.position = new Vector3(-8f, 0f, -5.2f);
                customerSpawnPoint = spawn.transform;
            }

            if (customerExitPoint == null)
            {
                GameObject exit = new GameObject("Aura_CustomerExitPoint");
                exit.transform.SetParent(transform);
                exit.transform.position = new Vector3(8f, 0f, -5.2f);
                customerExitPoint = exit.transform;
            }
        }

        private IEnumerator CustomerSpawnLoop()
        {
            while (true)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
                {
                    int freeSeatIndex = GetFreeSeatIndex();
                    if (freeSeatIndex != -1 && activeCustomers.Count < maxSimultaneousCustomers)
                    {
                        SpawnCustomerAtSeat(freeSeatIndex);
                    }
                }

                float delay = UnityEngine.Random.Range(minSpawnDelay, maxSpawnDelay);
                yield return new WaitForSeconds(delay);
            }
        }

        private int GetFreeSeatIndex()
        {
            for (int i = 0; i < seatOccupied.Length; i++)
            {
                if (!seatOccupied[i]) return i;
            }
            return -1;
        }

        private void SpawnCustomerAtSeat(int seatIndex)
        {
            seatOccupied[seatIndex] = true;

            // Pick a recipe from DeliveryManager or Menu
            MenuRecipeSO recipe = GetNextCustomerRecipe();
            if (recipe == null)
            {
                seatOccupied[seatIndex] = false;
                return;
            }

            GameObject customerObj;
            if (customerPrefab != null)
            {
                customerObj = Instantiate(customerPrefab, customerSpawnPoint.position, Quaternion.identity);
            }
            else
            {
                // Procedural stylized customer representation if prefab is not assigned
                customerObj = CreateProceduralCustomerObject();
                customerObj.transform.position = customerSpawnPoint.position;
            }

            Customer customer = customerObj.GetComponent<Customer>();
            if (customer == null) customer = customerObj.AddComponent<Customer>();
            if (customerObj.GetComponent<CustomerVisual>() == null) customerObj.AddComponent<CustomerVisual>();

            Vector3 seatPos = diningSeats[seatIndex].position;
            Vector3 exitPos = customerExitPoint.position;

            customer.Initialize(recipe, seatIndex, seatPos, exitPos);
            customer.OnCustomerServed += Customer_OnCustomerServed;
            customer.OnCustomerExpired += Customer_OnCustomerExpired;

            activeCustomers.Add(customer);
            OnCustomerArrived?.Invoke(this, customer);
        }

        private MenuRecipeSO GetNextCustomerRecipe()
        {
            // Pick a random recipe from available menu
            DeliveryManager dm = DeliveryManager.Instance;
            if (dm == null) return null;

            // We can reflect or read menu recipes
            var menuField = typeof(DeliveryManager).GetField("menu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (menuField != null)
            {
                MenuSO menu = menuField.GetValue(dm) as MenuSO;
                if (menu != null && menu.menuRecipes != null && menu.menuRecipes.Count > 0)
                {
                    return menu.menuRecipes[UnityEngine.Random.Range(0, menu.menuRecipes.Count)];
                }
            }
            return null;
        }

        private void DeliveryManager_OnOrderDelivered(object sender, MenuRecipeSO deliveredRecipe)
        {
            // Find a customer waiting for this recipe
            Customer targetCustomer = activeCustomers.Find(c => c.RequestedRecipe == deliveredRecipe && c.State == Customer.CustomerState.WaitingAtTable);
            if (targetCustomer != null)
            {
                targetCustomer.TryServe(deliveredRecipe, out int tip);
            }
        }

        private void Customer_OnCustomerServed(object sender, Customer.CustomerServedEventArgs e)
        {
            Customer customer = sender as Customer;
            if (customer != null)
            {
                happyCustomersCount++;
                totalTipsEarned += e.TipAmount;
                accumulatedRating += e.RatingScore;

                ReleaseSeat(customer.SeatIndex);
                activeCustomers.Remove(customer);

                OnTipsUpdated?.Invoke(this, totalTipsEarned);
                NotifyStatsUpdated();
                OnCustomerDeparted?.Invoke(this, customer);
            }
        }

        private void Customer_OnCustomerExpired(object sender, EventArgs e)
        {
            Customer customer = sender as Customer;
            if (customer != null)
            {
                missedCustomersCount++;
                ReleaseSeat(customer.SeatIndex);
                activeCustomers.Remove(customer);

                NotifyStatsUpdated();
                OnCustomerDeparted?.Invoke(this, customer);
            }
        }

        private void ReleaseSeat(int index)
        {
            if (index >= 0 && index < seatOccupied.Length)
            {
                seatOccupied[index] = false;
            }
        }

        private void NotifyStatsUpdated()
        {
            OnCustomerStatsUpdated?.Invoke(this, new CustomerServedStatsEventArgs
            {
                HappyCustomers = happyCustomersCount,
                MissedCustomers = missedCustomersCount,
                TotalTips = totalTipsEarned,
                AverageRating = AverageStarRating
            });
        }

        private GameObject CreateProceduralCustomerObject()
        {
            // Create a cute customer figure with body capsule and head sphere
            GameObject root = new GameObject("ProceduralCustomer");

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0, 0.85f, 0);
            body.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            // Customer cute chef / patron hat
            GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hat.name = "Hat";
            hat.transform.SetParent(root.transform);
            hat.transform.localPosition = new Vector3(0, 1.6f, 0);
            hat.transform.localScale = new Vector3(0.45f, 0.15f, 0.45f);

            // Overhead Canvas for thought bubble & patience
            GameObject canvasObj = new GameObject("OverheadCanvas");
            canvasObj.transform.SetParent(root.transform);
            canvasObj.transform.localPosition = new Vector3(0, 2.3f, 0);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 2f);

            return root;
        }
    }
}

