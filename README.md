## Queue System

Lightweight, extensible utilities to arrange and animate elements in a linear queue (stack/row/column) with dynamic gaps, per-element offsets, leader detection, locking, shifting, and optional two-element connectors.

### Install (Unity Package Manager via Git)
- Open Unity: Window → Package Manager → + → Add package from git URL…
- Use one of the following URLs (replace `owner/repo` with your GitHub path):
  - Using path query (recommended):
    - `https://github.com/owner/repo.git?path=/Packages/QueueSystem`
    - Pin to a tag/commit: `https://github.com/owner/repo.git?path=/Packages/QueueSystem#v1.0.0`
  - Using hash path (some older UPM versions):
    - `https://github.com/owner/repo.git#path=/Packages/QueueSystem`

Notes:
- This package includes an assembly definition `QueueSystem` and a `package.json` so it’s importable via UPM.
- Odin Inspector attributes are optional and wrapped with the `ODIN_INSPECTOR` define. If you use Odin, add the scripting define symbol `ODIN_INSPECTOR` in Project Settings → Player.

### Key Concepts
- **QueueController**: Owns the ordered list, computes target positions, animates elements, and emits events.
- **QueueElement**: Component for queued objects. Supports movement, locking, offsets, and index change notifications.
- **QueueElementConnector**: Keeps two elements connected as a pair and auto-shifts them together when space opens up.

### Dependencies
- Unity Events (`UnityEngine.Events`)
- Optional editor helpers use `Sirenix.OdinInspector` `[Button]` attributes (safe to remove if not using Odin)

### Serialized Settings (QueueController)
- `queueDirection (Vector3)`: Direction of growth for the queue (e.g., `Vector3.forward`, `Vector3.right`).
- `gapBetweenElements (float)`: Base spacing between consecutive elements.
- `tweenDuration (float)`: Time to animate movements when tweening is enabled.
- `updateOnStart (bool)`: If true, discovers `QueueElement` children at `Start()` and lays them out.

### Per-Element Offsets (QueueElement)
- `preLeaderOffset`: Additional offset applied before the leader (index 0).
- `preOffset`: Additional offset applied before non-leader elements.
- `postOffset`: Additional offset applied after each element, affecting the distance to the following element.

### Events
- `QueueController.onElementPositionsUpdated`: Fired when all queued movements complete (or instantly when not tweening).
- `QueueController.OnElementBecomeLeader (QueueElement)`: Fired after a new leader settles into place. Its matching `QueueElement.onBecomeLeader` is also invoked.
- `QueueElement.onPreBecomeLeader`: Fired just before an element is about to become leader (pre-move signal).
- `QueueController.OnElementsShifted`: Fired when the internal list is compacted (e.g., after removals and shifts).

### How It Works
1. Elements are registered to a `QueueController` in order.
2. `GetElementPosition(index)` computes each element's target local position from:
   - `queueDirection`
   - `gapBetweenElements`
   - Each element's `preLeaderOffset`, `preOffset`, and `postOffset`
3. `UpdatePositions(tween: bool)` sets or animates movement to targets. When tweening, movement occurs over `tweenDuration` and completion triggers `onElementPositionsUpdated`.
4. Leader changes are anticipated (`onPreBecomeLeader`) and confirmed (`onBecomeLeader`, `OnElementBecomeLeader`) only when motion has settled.
5. Removing elements leaves `null` holes; `ShiftUnlockedElements()` compacts until a locked element is encountered and trims trailing `null` entries.

### Setup
1. Add a `QueueController` to a parent GameObject.
2. Add `QueueElement` to each child you want managed by the queue.
3. Either:
   - Tick `updateOnStart` to auto-register children at runtime, or
   - Call `UpdateQueueFromChildren()` yourself, or
   - Register programmatically via `AddElement`/`InsertElement`/`SetElement`.
4. Optionally, add `QueueElementConnector` somewhere in the scene to connect two `QueueElement`s that share the same index.

### API Overview
- `QueueController : MonoBehaviour`
  - Fields: `queueDirection`, `gapBetweenElements`, `onElementPositionsUpdated`, `OnElementBecomeLeader`, `OnElementsShifted`
  - `void AddElement(QueueElement element, bool updatePositions)`
  - `void InsertElement(QueueElement element, bool updatePositions, int index)`
  - `void SetElement(QueueElement element, bool updatePositions, int index)` (sets or appends at `index`)
  - `void RemoveElement(QueueElement element)` / `void RemoveElements(QueueElement[] elements)`
  - `void DestroyElements()` (calls `QueueElement.Destroy()` for all and clears)
  - `void UpdatePositions(bool tween)`
  - `Vector3 GetElementPosition(int index)` / `Vector3 CalulateElementPositionWithIndex(int index)`
  - `QueueElement GetLeader()` / `bool IsLeader(QueueElement)`
  - `void ShiftUnlockedElements()` / `bool ShiftElement(QueueElement element, int count)`
  - `int CountElements()` / `QueueElement GetElement(int index)` / `QueueElement[] GetElements()`
  - `int GetElementIndex(QueueElement element)` / `bool InQueue(QueueElement element)`
  - `void UpdateQueueFromChildren()` (runtime) and `UpdateQueueFromChildrenEditor()` (editor-only button)

- `QueueElement : MonoBehaviour`
  - Events: `onPreBecomeLeader`, `onBecomeLeader`, `Action<int> OnIndexChanged`
  - Offsets: `preLeaderOffset`, `preOffset`, `postOffset`
  - State: `bool IsLocked`, `bool IsMoving()`, `bool InQueue()`
  - Movement: `void MoveToPosition(Vector3 localPosition, float duration)`, `bool IsAtDestination()`
  - Index/util: `int GetIndex()`, `Vector3 GetDestinationPosition()`
  - Locking: `void LockElement()`, `void UnlockElement()`
  - Shifting: `int CountEmptySpacesAfterElement()`, `void ForceShiftElement(int shiftCount)`
  - Lifecycle: `void AssignController(QueueController)`, `void RemoveController()`, `void Destroy()`, `void ResetStates()`

- `QueueElementConnector : MonoBehaviour`
  - `void Connect(QueueElement a, QueueElement b)`
  - `void DestroyConnection()` / `void Destroy()`
  - Auto-updates its world position to the midpoint of connected elements (LateUpdate)
  - Listens to `OnElementsShifted` and triggers paired `ForceShiftElement` so both move together

### Examples

Register elements programmatically and animate layout:
```csharp
public class QueueBootstrap : MonoBehaviour
{
    [SerializeField] private QueueSystem.QueueController controller;
    [SerializeField] private QueueSystem.QueueElement[] elements;

    private void Start()
    {
        foreach (var e in elements)
        {
            controller.AddElement(e, updatePositions: false);
        }
        controller.UpdatePositions(tween: true);
    }
}
```

React to leader changes:
```csharp
controller.OnElementBecomeLeader.AddListener(newLeader =>
{
    Debug.Log($"Leader is now: {newLeader.name}");
});
```

Remove elements and compact the queue:
```csharp
void RemoveAndCompact(QueueSystem.QueueElement element)
{
    controller.RemoveElement(element);
    controller.UpdatePositions(tween: true);
}
```

Lock an element to prevent compaction from passing through it:
```csharp
element.LockElement();
// Later
element.UnlockElement();
```

Connect two elements that share the same index:
```csharp
// Both must currently resolve to the same queue index
connector.Connect(elementA, elementB);
```

Shift an element left by available empty spaces:
```csharp
// Will attempt to move across `count` empty slots toward the leader
bool success = controller.ShiftElement(element, count: 1);
if (success) controller.UpdatePositions(tween: true);
```

### Best Practices
- Use `UpdateQueueFromChildren()` when building queues from existing hierarchy children.
- Prefer `RemoveElement` / `RemoveElements` over directly destroying GameObjects; the controller will compact the list and fire events.
- Use `onPreBecomeLeader` for pre-leader effects and `onBecomeLeader`/`OnElementBecomeLeader` for confirmed leader transitions.
- Keep `preLeaderOffset`/`preOffset`/`postOffset` small and intentional; they are cumulative across the queue.
- When using `QueueElementConnector`, ensure both elements truly belong to the same logical slot (same index) before calling `Connect`.

### Troubleshooting
- "Element already exists in the queue": The same `QueueElement` was added twice.
- "Index is out of bounds": Ensure `InsertElement`/`SetElement` indices are within `0..Count`.
- "Element not found in the queue": The element was not registered to this controller.
- "Not enough space to shift": There is a non-null element blocking the requested shift distance.
- "Can't connect slots with different indexes": Only connect elements that currently resolve to the same index.
- `Controller is null` (from `QueueElement.IsLeader()`): Ensure the element has been assigned to a controller.



