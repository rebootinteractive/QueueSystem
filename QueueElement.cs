using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace QueueSystem
{
    public class QueueElement: MonoBehaviour
    {
        /// <summary>
        /// On pre become leader event is called when this element is about to become the leader of the queue. This is triggered before the element is moved to the leader position.
        /// </summary>
        public UnityEvent onPreBecomeLeader;
        
        /// <summary>
        /// On become leader event is called when this element becomes the leader of the queue. This is triggered after the element is moved to the leader position.
        /// </summary>
        public UnityEvent onBecomeLeader;
        public QueueController Controller { get; private set; }
        private bool _wasLeader;
        protected Coroutine MoveCoroutine;

        public float preOffset;
        public float preLeaderOffset;
        public float postOffset;
        
        public bool IsLocked { get; private set; }
        [SerializeField] private bool destroyGameObjectOnDestroy=true;

        public Action<int> OnIndexChanged;
        
        public virtual void AssignController(QueueController controller)
        {
            Controller = controller;
            controller.onElementPositionsUpdated.AddListener(OnElementPositionsUpdated);
        }

        protected virtual void OnElementPositionsUpdated()
        {
            if(Controller==null) return;
            
            if (Controller.IsLeader(this) && !_wasLeader)
            {
                _wasLeader = true;
                onBecomeLeader?.Invoke();
                Controller.ElementBecomeLeader(this);
            }
            OnIndexChanged?.Invoke(GetIndex());
        }

        public virtual void Destroy()
        {
            if(destroyGameObjectOnDestroy){
                Destroy(gameObject);
            }
            if (Controller != null) 
                Controller.RemoveElement(this);
            StopActiveMovement();
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            if(MoveCoroutine!=null) StopCoroutine(MoveCoroutine);
        }

        public virtual void MoveToPosition(Vector3 localPosition,float duration)
        {
            StopActiveMovement();
            MoveCoroutine = StartCoroutine(MoveToPositionCoroutine(localPosition, duration));
        }
        
        public void StopActiveMovement()
        {
            if (MoveCoroutine!=null)
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
            if(Controller==null){
                Debug.LogError("Controller is null");
                return false;
            }
            return Controller.IsLeader(this);
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
            return Controller.GetElementIndex(this);
        }
        
        public int CountEmptySpacesAfterElement()
        {
            if(Controller==null) {                
                return 0;
            }
            return Controller.CountEmptySpacesAfterElement(this);
        }
        
        public void ForceShiftElement(int shiftCount)
        {
            Controller.ShiftElement(this, shiftCount);
            Controller.ShiftUnlockedElements();
            Controller.UpdatePositions(true);
        }

        public bool InQueue()
        {
            return Controller.InQueue(this);
        }

        public bool IsMoving()
        {
            return MoveCoroutine != null;
        }

        public bool IsAtDestination()
        {
            var currentPosition = transform.localPosition;
            var destinationPosition = Controller.GetElementPosition(GetIndex());
            return Vector3.SqrMagnitude(currentPosition - destinationPosition) < 0.001f;
        }

        public void RemoveController()
        {
            if (Controller)
            {
                Controller.onElementPositionsUpdated.RemoveListener(OnElementPositionsUpdated);
                Controller = null;
            }
        }
        
        public Vector3 GetDestinationPosition()
        {
            return Controller.GetElementPosition(GetIndex());
        }

        public void ResetStates(){
            _wasLeader=false;
            IsLocked=false;
            StopActiveMovement();
            RemoveController();
        }
    }
}