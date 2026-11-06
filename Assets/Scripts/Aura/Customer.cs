using System;
using System.Collections;
using UnityEngine;

namespace AuraKitchen
{
    public class Customer : MonoBehaviour
    {
        public enum CustomerState
        {
            Entering,
            WaitingAtTable,
            Eating,
            LeavingHappy,
            LeavingAngry
        }

        public event EventHandler<float> OnPatienceRatioChanged;
        public event EventHandler<CustomerServedEventArgs> OnCustomerServed;
        public event EventHandler OnCustomerExpired;

        public class CustomerServedEventArgs : EventArgs
        {
            public int TipAmount;
            public float RatingScore;
            public MenuRecipeSO Recipe;
        }

        [Header("Settings")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float defaultPatienceSeconds = 55f;
        [SerializeField] private float eatingDurationSeconds = 3.5f;
        [SerializeField] private int baseTip = 15;

        private CustomerState currentState = CustomerState.Entering;
        private MenuRecipeSO requestedRecipe;
        private Vector3 targetSeatPosition;
        private Vector3 exitPosition;
        private float maxPatience;
        private float currentPatience;
        private int seatIndex = -1;
        private bool isDelivered = false;

        public CustomerState State => currentState;
        public MenuRecipeSO RequestedRecipe => requestedRecipe;
        public float PatienceRatio => maxPatience > 0 ? Mathf.Clamp01(currentPatience / maxPatience) : 0f;
        public int SeatIndex => seatIndex;

        public void Initialize(MenuRecipeSO recipe, int seatIdx, Vector3 seatPos, Vector3 exitPos, float patienceModifier = 1f)
        {
            requestedRecipe = recipe;
            seatIndex = seatIdx;
            targetSeatPosition = seatPos;
            exitPosition = exitPos;
            maxPatience = defaultPatienceSeconds * Mathf.Max(0.5f, patienceModifier);
            currentPatience = maxPatience;
            currentState = CustomerState.Entering;

            StartCoroutine(CustomerLifecycleRoutine());
        }

        private IEnumerator CustomerLifecycleRoutine()
        {
            // 1. Move to seat
            while (Vector3.Distance(transform.position, targetSeatPosition) > 0.15f)
            {
                MoveTowards(targetSeatPosition);
                yield return null;
            }
            transform.position = targetSeatPosition;
            currentState = CustomerState.WaitingAtTable;

            // 2. Wait for order with patience countdown
            while (!isDelivered && currentPatience > 0f)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
                {
                    currentPatience -= Time.deltaTime;
                    OnPatienceRatioChanged?.Invoke(this, PatienceRatio);
                }
                yield return null;
            }

            // 3. Check delivery or timeout
            if (isDelivered)
            {
                currentState = CustomerState.Eating;
                yield return new WaitForSeconds(eatingDurationSeconds);
                currentState = CustomerState.LeavingHappy;
            }
            else
            {
                currentState = CustomerState.LeavingAngry;
                OnCustomerExpired?.Invoke(this, EventArgs.Empty);
            }

            // 4. Move to exit and despawn
            while (Vector3.Distance(transform.position, exitPosition) > 0.3f)
            {
                MoveTowards(exitPosition);
                yield return null;
            }

            Destroy(gameObject);
        }

        public bool TryServe(MenuRecipeSO deliveredRecipe, out int tipEarned)
        {
            tipEarned = 0;
            if (currentState != CustomerState.WaitingAtTable || isDelivered) return false;
            if (deliveredRecipe != requestedRecipe) return false;

            isDelivered = true;
            float ratio = PatienceRatio;

            // Calculate tip based on patience left
            if (ratio > 0.65f)
            {
                tipEarned = Mathf.RoundToInt(baseTip * 1.5f); // Great service!
            }
            else if (ratio > 0.30f)
            {
                tipEarned = baseTip; // Normal service
            }
            else
            {
                tipEarned = Mathf.Max(2, Mathf.RoundToInt(baseTip * 0.4f)); // Slow service
            }

            OnCustomerServed?.Invoke(this, new CustomerServedEventArgs
            {
                TipAmount = tipEarned,
                RatingScore = ratio * 5f,
                Recipe = deliveredRecipe
            });

            return true;
        }

        private void MoveTowards(Vector3 destination)
        {
            Vector3 direction = (destination - transform.position);
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }
    }
}

