using UnityEngine;

namespace QueueSystem
{
    public class QueueElementConnector : MonoBehaviour
    {
        public QueueElement Element1 { get; private set; }
        public QueueElement Element2 { get; private set; }
        

        public virtual void Connect(QueueElement element1, QueueElement element2)
        {
            transform.position = (element1.transform.position + element2.transform.position) / 2;
            
            if (Application.isPlaying == false)
            {
                return;
            }
            
            int slot1Index = element1.GetIndex();
            int slot2Index = element2.GetIndex();

            if (slot1Index != slot2Index)
            {
                Debug.LogError("Can't connect slots with different indexes");
                return;
            }

            Element1 = element1;
            Element2 = element2;

            //Lock both slots 
            Element1.LockElement();
            Element2.LockElement();

            Element1.QueueController.OnElementsShifted += OnElementShifted;
            Element2.QueueController.OnElementsShifted += OnElementShifted;
            
            UpdatePosition();
        }

        public virtual void DestroyConnection()
        {
            Element1.QueueController.OnElementsShifted -= OnElementShifted;
            Element2.QueueController.OnElementsShifted -= OnElementShifted;
            Destroy();
        }

        public virtual void Destroy()
        {
            Destroy(gameObject);
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        protected virtual void UpdatePosition()
        {
            if (Element1 == null || Element2 == null) return;
            var middlePoint = (Element1.transform.position + Element2.transform.position) / 2;
            transform.position = middlePoint;
        }

        protected void OnElementShifted()
        {
            int connectedSlot1AvailableShift = Element1.CountEmptySpacesAfterElement();
            int connectedSlot2AvailableShift = Element2.CountEmptySpacesAfterElement();

            int shift = Mathf.Min(connectedSlot1AvailableShift, connectedSlot2AvailableShift);
            if (shift > 0)
            {
                Element1.QueueController.OnElementsShifted -= OnElementShifted;
                Element2.QueueController.OnElementsShifted -= OnElementShifted;

                Element1.ForceShiftElement(shift);
                Element2.ForceShiftElement(shift);

                Element1.QueueController.OnElementsShifted += OnElementShifted;
                Element2.QueueController.OnElementsShifted += OnElementShifted;
            }
        }
    }
}