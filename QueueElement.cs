using System;
using System.Collections;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
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
            if (QueueController != null)
                QueueController.RemoveElement(this);
            StopActiveMovement();
            StopAllCoroutines();
            if (destroyGameObjectOnDestroy)
            {
                Destroy(gameObject);
            }
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



        #if ODIN_INSPECTOR
        [Button]
        #endif
        public void LockElement()
        {
            IsLocked = true;
        }

        #if ODIN_INSPECTOR
        [Button]
        #endif
        public void UnlockElement()
        {
            IsLocked = false;
        }

        public int GetIndex()
        {
            if (QueueController == null)
            {
                Debug.LogError("Controller is null");
                return -1;
            }
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
            if (QueueController == null)
            {
                Debug.LogError("Controller is null");
                return;
            }
            QueueController.ShiftElement(this, shiftCount);
            QueueController.ShiftUnlockedElements();
            QueueController.UpdatePositions(true);
        }

        public bool InQueue()
        {
            if (QueueController == null) return false;
            return QueueController.InQueue(this);
        }

        public bool IsMoving()
        {
            return MoveCoroutine != null;
        }

        public bool IsAtDestination()
        {
            if (QueueController == null) return true;
            var currentPosition = transform.localPosition;
            var destinationPosition = QueueController.GetElementPosition(GetIndex());
            return Vector3.SqrMagnitude(currentPosition - destinationPosition) < 0.001f;
        }

        public Vector3 GetDestinationPosition()
        {
            if (QueueController == null)
            {
                Debug.LogError("Controller is null");
                return transform.localPosition;
            }
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