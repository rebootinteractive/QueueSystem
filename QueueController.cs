using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace QueueSystem
{
    public class QueueController : MonoBehaviour
    {
        public Vector3 queueDirection = Vector3.forward;
        [SerializeField] private bool getChildrenOnStart;       
        [SerializeField] private bool ignoreGrandchildren;
        [SerializeField] protected float gapBetweenElements = 1f;
        [SerializeField] private float tweenDuration = 10f;

        public UnityEvent<QueueElement> OnElementBecomeLeader;
        public UnityEvent onElementPositionsUpdated;

        public Action OnElementsShifted;

        [SerializeField, Unity.Collections.ReadOnly]
        protected List<QueueElement> queueElements = new List<QueueElement>();
        private QueueElement _lastLeader;
        private QueueElement _pendingLeader;
        private Coroutine _updatePositionsCoroutine;


        protected virtual void Start()
        {
            if (getChildrenOnStart)
                UpdateQueueFromChildren();
        }
        
        public virtual void DestroyElements()
        {
            var elementsToDestroy = queueElements.Where(e => e != null).ToList();            
            foreach (var element in elementsToDestroy)
            {
                element.Destroy();
            }
            queueElements.Clear();
        }

        public virtual void ClearElements()
        {
            var elementsToClear = queueElements.Where(e => e != null).ToList();
            var elementsToRemove = new List<QueueElement>();
            foreach (var element in queueElements)
            {
                elementsToRemove.Add(element);    
            }

            foreach (var element in elementsToRemove)
            {
                RemoveElement(element);
            }
            
        }

        public virtual void AddElement(QueueElement element, bool updatePositions)
        {
            if (queueElements.Contains(element))
            {
                Debug.LogError("Element already exists in the queue");
                return;
            }

            queueElements.Add(element);
            element.AssignController(this);
            CheckLeader();
            if (updatePositions) UpdateElementPosition(element);
        }

        public virtual void InsertElement(QueueElement element, bool updatePositions, int index)
        {
            if(index < 0 || index > queueElements.Count){
                Debug.LogError("Index is out of bounds");
                return;
            }
            
            if(queueElements.Contains(element))
            {
                Debug.LogError("Element already exists in the queue");
                return;
            }

            queueElements.Insert(index, element);
            element.AssignController(this);
            CheckLeader();
            if (updatePositions) UpdateElementPosition(element);
            
        }

        public virtual void SetElement(QueueElement element, bool updatePositions, int index)
        {
            if (index < 0 || index > queueElements.Count)
            {
                Debug.LogError("Index is out of bounds");
                return;
            }

            if (queueElements.Contains(element))
            {
                Debug.LogError("Element already exists in the queue");
                return;
            }

            if (index == queueElements.Count)
            {
                queueElements.Add(element);
            }
            else
            {
                queueElements[index] = element;
            }

            element.AssignController(this);

            CheckLeader();
            if (updatePositions) UpdateElementPosition(element);
            
        }

        public virtual void ElementBecomeLeader(QueueElement element)
        {
            OnElementBecomeLeader?.Invoke(element);
        }

        public virtual void RemoveElement(QueueElement element)
        {
            int index = queueElements.IndexOf(element);
            if (index < 0)
            {
                Debug.LogError("Element not found in the queue");
                return;
            }
            
            queueElements[index] = null;
            element.ResetStates();

            ShiftUnlockedElements();
            CheckLeader();            
        }

        public virtual void RemoveElements(QueueElement[] elements)
        {
            foreach (var element in elements)
            {
                int index = queueElements.IndexOf(element);
                if (index == -1) continue;
                queueElements[index] = null;
            }

            ShiftUnlockedElements();
            CheckLeader();        
            UpdatePositions(true);
        }

        protected virtual void CheckLeader()
        {
            var currentLeader = GetLeader();
            if (currentLeader == _lastLeader) return;
            _lastLeader = currentLeader;
            _pendingLeader = currentLeader;
            if (currentLeader != null)
            {
                currentLeader.onPreBecomeLeader?.Invoke();
            }
        }

        /// <summary>
        /// Shifts the elements in the queue to fill the empty spaces until any locked element
        /// </summary>
        public void ShiftUnlockedElements()
        {
            bool lockedFound = false;

            for (int i = 0; i < queueElements.Count - 1; i++)
            {
                if (queueElements[i] == null)
                {
                    for (int j = i + 1; j < queueElements.Count; j++)
                    {
                        if (queueElements[j] != null)
                        {
                            if (queueElements[j].IsLocked)
                            {
                                lockedFound = true;
                                break;
                            }
                            queueElements[i] = queueElements[j];
                            queueElements[j] = null;
                            break;
                        }
                    }

                    if (lockedFound) break;
                }
            }

            OnElementsShifted?.Invoke();
            TrimQueue();
        }

        private void TrimQueue()
        {
            int nullCount = 0;
            for (int i = queueElements.Count - 1; i >= 0; i--)
            {
                if (queueElements[i] == null)
                {
                    nullCount++;
                }
                else
                {
                    break;
                }
            }

            if (nullCount > 0)
            {
                int newCount = queueElements.Count - nullCount;
                if (newCount < 0) newCount = 0;
                if (nullCount > 0)
                {
                    queueElements.RemoveRange(newCount, queueElements.Count - newCount);
                }
            }
        }

        public bool ShiftElement(QueueElement element, int count)
        {
            int index = queueElements.IndexOf(element);
            if (index == -1) return false;

            // Shift the element until all further empty spaces are filled or count is reached
            // Ignores element being locked
            for (int i = index - 1; i >= 0; i--)
            {
                if (queueElements[i] == null)
                {
                    count--;
                    if (count == 0)
                    {
                        queueElements[i] = element;
                        queueElements[index] = null;
                        return true;
                    }
                }
                else
                {
                    Debug.LogError("Not enough space to shift");
                }
            }

            return false;

        }


        /// <summary>
        /// How many empty spaces are available after the element
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public int CountEmptySpacesAfterElement(QueueElement element)
        {
            int index = queueElements.IndexOf(element);
            if (index == -1) return 0;
            int nullCount = 0;
            for (int i = index - 1; i >= 0; i--)
            {
                if (queueElements[i] == null)
                {
                    nullCount++;
                }
                else
                {
                    break;
                }
            }

            return nullCount;
        }


        #if ODIN_INSPECTOR
        [Button]
        #endif
        public virtual void UpdatePositions(bool tween)
        {
            if (queueElements.Count == 0)
                return;

            if (tween)
            {
                if (_updatePositionsCoroutine != null)
                {
                    StopCoroutine(_updatePositionsCoroutine);
                }
                _updatePositionsCoroutine = StartCoroutine(UpdatePositionsCoroutine());
            }
            else
            {
                for (int i = 0; i < queueElements.Count; i++)
                {
                    if (queueElements[i] == null) continue;
                    queueElements[i].transform.localPosition = GetElementPosition(i);
                }

                if (Application.isPlaying)
                    onElementPositionsUpdated.Invoke();
            }
        }

        private void UpdateElementPosition(QueueElement element)
        {
            int index = queueElements.IndexOf(element);
            if (index == -1)
            {
                Debug.LogError("Element not found in the queue");
                return;
            }

            element.transform.localPosition = GetElementPosition(index);
        }

        /// <summary>
        /// Returns the position of the element in the queue
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public virtual Vector3 GetElementPosition(int index)
        {
            //If index is not valid calculate it
            if(index<0 || index>=queueElements.Count)
                return CalulateElementPositionWithIndex(index);
            
            Vector3 position = Vector3.zero;
            
            // Apply gap offsets of all previous elements
            for (int i = 0; i < index; i++)
            {
                float gap = gapBetweenElements;
                if (queueElements[i] != null)
                {
                    if(i==0)
                        gap += queueElements[i].preLeaderOffset;
                    else
                        gap += queueElements[i].preOffset;

                    gap += queueElements[i].postOffset;
                }
                position += queueDirection * gap;
            }
            
            // Apply the current element's gap offset if current element is not leader
            if (queueElements[index] != null)
            {
                if(index==0)
                    position += queueDirection * queueElements[index].preLeaderOffset;
                else
                    position += queueDirection * queueElements[index].preOffset;
            }
            
            return position;
        }

        public virtual Vector3 CalulateElementPositionWithIndex(int index)
        {
            return queueDirection * gapBetweenElements * index;
        }

        protected virtual IEnumerator UpdatePositionsCoroutine()
        {
            for (int index = 0; index < queueElements.Count; index++)
            {
                QueueElement element = queueElements[index];
                if (element == null) continue;

                var localPosition = GetElementPosition(index);
                element.MoveToPosition(localPosition, tweenDuration);
            }

            while (AnyQueueElementsMoving()) yield return null;

            onElementPositionsUpdated.Invoke();

            if (_pendingLeader != null)
            {
                // Post-leader event after positions settle
                _pendingLeader.onBecomeLeader?.Invoke();
                OnElementBecomeLeader?.Invoke(_pendingLeader);
                _pendingLeader = null;
            }
            _updatePositionsCoroutine = null;

            // if(queueElements[0]!=null && queueElements[0].IsAtDestination()==false)
            // {
            //     UpdatePositions(true);
            // }
        }

        public bool AnyQueueElementsMoving()
        {
            foreach (var element in queueElements)
            {
                if (element == null) continue;
                if (element.IsMoving()) return true;
            }

            return false;
        }


        public bool IsLeader(QueueElement queueElement)
        {
            return queueElements.Count > 0 && queueElements[0] == queueElement;
        }

        public virtual void UpdateQueueFromChildren()
        {
            var childElements = GetComponentsInChildren<QueueElement>();
            if (ignoreGrandchildren)
            {
                childElements = childElements.Where(e => e.transform.parent == transform).ToArray();
            }
            queueElements = new List<QueueElement>(childElements.Length);

            for (int index = 0; index < childElements.Length; index++)
            {
                QueueElement element = childElements[index];
                queueElements.Add(element);
                element.AssignController(this);
            }

            _lastLeader = GetLeader();
            _pendingLeader = _lastLeader;
            if (_pendingLeader != null)
            {
                _pendingLeader.onPreBecomeLeader?.Invoke();
            }
            UpdatePositions(false);
        }


        public int GetElementIndex(QueueElement queueElement)
        {
            return queueElements.IndexOf(queueElement);
        }

        public int CountElements()
        {
            return queueElements.Count;
        }

        public QueueElement GetElement(int index)
        {
            return queueElements[index];
        }

        public QueueElement[] GetElements()
        {
            return queueElements.ToArray();
        }

        public bool InQueue(QueueElement queueElement)
        {
            return queueElements.IndexOf(queueElement) != -1;
        }

        public QueueElement GetLeader()
        {
            if (queueElements.Count == 0) return null;
            return queueElements[0];
        }

        public List<QueueElement> GetForeElements(QueueElement element)
        {
            List<QueueElement> foreElements = new List<QueueElement>();
            int index = queueElements.IndexOf(element);
            if (index == -1) return foreElements;
            for (int i = 0; i < index; i++)
            {
                foreElements.Add(queueElements[i]);
            }

            return foreElements;
        }

#if UNITY_EDITOR

        #if ODIN_INSPECTOR
        [Button]
        #endif
        public void UpdateQueueFromChildrenEditor()
        {
            if (Application.isPlaying) return;

            var childElements = GetComponentsInChildren<QueueElement>();
            if (ignoreGrandchildren)
            {
                childElements = childElements.Where(e => e.transform.parent == transform).ToArray();
            }
            queueElements = new List<QueueElement>(childElements.Length);
            for (int index = 0; index < childElements.Length; index++)
            {
                QueueElement element = childElements[index];
                queueElements.Add(element);
                element.transform.localPosition = GetElementPosition(index);
                UnityEditor.EditorUtility.SetDirty(element.gameObject);
            }

            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

    
#endif

    }
}