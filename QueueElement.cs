using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace QueueSystem
{
    public class QueueElement : MonoBehaviour
    {
        /// <summary>
        /// On pre become leader event is called when this element is about to become the leader of the queue. This is triggered before the element is moved to the leader position.
        /// </summary>
        public UnityEvent onPreBecomeLeader;

        /// <summary>
        /// On become leader event is called when this element becomes the leader of the queue. This is triggered after the element is moved to the leader position.
        /// </summary>
        public UnityEvent onBecomeLeader;
        public QueueController QueueController { get; private set; }
        protected Coroutine MoveCoroutine;

        public float preOffset;
        public float preLeaderOffset;
        public float postOffset;

        public bool IsLocked { get; private set; }
        [SerializeField] private bool destroyGameObjectOnDestroy = true;

        public Action<int> OnIndexChanged;

        public virtual void AssignController(QueueController controller)
        {
            QueueController = controller;
            controller.onElementPositionsUpdated.AddListener(OnElementPositionsUpdated);
        }

        protected virtual void OnElementPositionsUpdated()
        {
            if (QueueController == null) return;
            OnIndexChanged?.Invoke(GetIndex());
        }

        public virtual void Destroy()
        {
            if (destroyGameObjectOnDestroy)
            {
                Destroy(gameObject);
            }
            if (QueueController != null)
                QueueController.RemoveElement(this);
            StopActiveMovement();
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            if (MoveCoroutine != null) StopCoroutine(MoveCoroutine);
        }

        public virtual void MoveToPosition(Vector3 localPosition, float duration)
        {
            StopActiveMovement();
            MoveCoroutine = StartCoroutine(MoveToPositionCoroutine(localPosition, duration));
        }

        public void StopActiveMovement()
        {
            if (MoveCoroutine != null)
            {
                StopCoroutine(MoveCoroutine);
                MoveCoroutine = null;
            }
        }

        protected virtual IEnumerator MoveToPositionCoroutine(Vector3 localPosition, float duration)
        {
            float elapsedTime = 0;
            Vector3 startPosition = transform.localPosition;
            while (elapsedTime < duration)
            {
                transform.localPosition = Vector3.Lerp(startPosition, localPosition, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = localPosition;
            MoveCoroutine = null;
        }

        public bool IsLeader()
        {
            if (QueueController == null)
            {
                Debug.LogError("Controller is null");
                return false;
            }
            return QueueController.IsLeader(this);
        }



        [Button]
        public void LockElement()
        {
            IsLocked = true;
        }

        [Button]
        public void UnlockElement()
        {
            IsLocked = false;
        }

        public int GetIndex()
        {
            return QueueController.GetElementIndex(this);
        }

        public int CountEmptySpacesAfterElement()
        {
            if (QueueController == null)
            {
                return 0;
            }
            return QueueController.CountEmptySpacesAfterElement(this);
        }

        public void ForceShiftElement(int shiftCount)
        {
            QueueController.ShiftElement(this, shiftCount);
            QueueController.ShiftUnlockedElements();
            QueueController.UpdatePositions(true);
        }

        public bool InQueue()
        {
            return QueueController.InQueue(this);
        }

        public bool IsMoving()
        {
            return MoveCoroutine != null;
        }

        public bool IsAtDestination()
        {
            var currentPosition = transform.localPosition;
            var destinationPosition = QueueController.GetElementPosition(GetIndex());
            return Vector3.SqrMagnitude(currentPosition - destinationPosition) < 0.001f;
        }

        public Vector3 GetDestinationPosition()
        {
            return QueueController.GetElementPosition(GetIndex());
        }

        public void ResetStates()
        {
            IsLocked = false;
            StopActiveMovement();
            if (QueueController)
            {
                QueueController.onElementPositionsUpdated.RemoveListener(OnElementPositionsUpdated);
                QueueController = null;
            }
        }

        public void RemoveFromQueue()
        {
            if (QueueController)
            {
                QueueController.RemoveElement(this);
            }
        }
    }
}