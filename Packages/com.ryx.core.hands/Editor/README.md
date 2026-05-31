# Grab Animation Editor Tool

A Unity Editor toolset for previewing and configuring grab animations without entering Play Mode.

## Overview

This tool allows you to:
- Preview hand grab animations directly in the Scene view
- Configure grab points interactively with visual feedback
- Test both left and right hand orientations
- Adjust animation playback with loop and speed controls

---

## Getting Started

### Opening the Editor

**Option 1: From Menu**
- Go to `Window > Grab Animation Editor`

**Option 2: From Inspector**
- Select a GameObject with a `GrabItem` component
- Click the **"Preview Animation"** button in the inspector
- This will open (or focus) the Grab Animation Editor with the item pre-selected

---

## Grab Animation Editor Window

### Properties Panel (Right Side)

| Property | Description |
|----------|-------------|
| **Grab Item** | The item to preview (auto-selects from Scene selection) |
| **Animation Clip** | The grab animation to play (auto-finds `Grab_[ItemID]` clips) |
| **Hand Side** | Choose between Left or Right hand preview |

### Preview Settings

| Setting | Description |
|---------|-------------|
| **Auto-select from Scene** | Automatically select GrabItems when clicked in Scene |
| **Show Path Preview** | Display the grab path trajectory |
| **Show Hand Gizmo** | Show hand orientation gizmo at grab point |

### Playback Controls

| Button | Function |
|--------|----------|
| **▶ / ⏸** | Play / Pause animation |
| **⏮** | Step backward one frame |
| **⏭** | Step forward one frame |
| **⏹** | Reset to beginning |
| **Loop** | Toggle animation looping |
| **Speed** | Adjust playback speed (0.1x - 3.0x) |

### Playback Behavior
- When **Loop is disabled** and animation reaches the end, playback stops automatically
- Pressing **Play** while at the end restarts from the beginning
- The timeline slider allows scrubbing to any point in the animation

---

## GrabItem Inspector Enhancements

When you select a GameObject with a `GrabItem` component, the inspector provides additional tools:

### Visualization Settings

| Option | Description |
|--------|-------------|
| **Show Grab Point** | Display grab point sphere and orientation arrows |
| **Show Hand Preview** | Show wireframe hand at grab point |
| **Show Path Preview** | Display the grab path from camera to item |
| **Live Animation Preview** | Real-time animation update when moving grab point |
| **Gizmo Color** | Customize visualization colors |

### Alignment & Snapping Tools

#### Snapping
- **Grid Snap**: Snap position to configurable grid (default: 0.1 units)
- **Rotation Snap**: Snap rotation to angle increments (default: 15°)

#### Quick Alignment Buttons
| Button | Action |
|--------|--------|
| **Center on Object** | Move grab point to object's center |
| **Snap to Surface** | Raycast down and align to nearest surface |
| **Align to World** | Reset rotation to world alignment |
| **Align to Parent** | Match parent transform's rotation |

### Scene View Handles
- **Move Handle**: Drag to reposition the grab point
- **Rotation Handle**: Hold **Shift** + drag to rotate the grab point

---

## Temporary Preview Hands

The tool creates temporary preview hands that:
- Are **never saved** in the scene (uses `HideFlags.HideAndDontSave`)
- Are automatically **cleaned up** when the editor window closes
- Support **left hand mirroring** (uses negative Y scale)
- Show the actual hand model from your project

---

## Animation Naming Convention

The tool automatically searches for animations named:
```
Grab_[ItemID]
```

For example, if your `GrabItem.ItemID` is "Tap1", the tool looks for:
- `Grab_Tap1`
- `A_Hand_Grab_Tap1`
- Or similar variations

---

## Workflow Example

1. **Select** a GrabItem in the Scene
2. **Click** "Preview Animation" in the inspector
3. The Grab Animation Editor opens with the item selected
4. **Choose** Left or Right hand from the dropdown
5. **Press Play** to see the grab animation
6. **Adjust** the grab point position in Scene view
7. Changes are reflected in real-time if "Live Animation Preview" is enabled

---

## Tips

- Use **Shift + drag** in Scene view to rotate the grab point
- Enable **Grid Snap** for precise positioning
- The **validation section** in the inspector shows if animations are missing
- Preview hands are colored cyan by default for visibility
- The hand wireframe shows finger orientation even without animation

---

## File Structure

```
Editor/
├── GrabAnimationEditorWindow.cs   # Main editor window
├── GrabAnimationPreviewController.cs  # Preview logic & temp hand management
├── GrabItemEditor.cs              # Custom inspector for GrabItem
├── Shared.Hands.Editor.asmdef     # Assembly definition
└── README.md                      # This file
```

---

## Requirements

- Unity 2021.3 or later
- Splines package (for path preview)
- A hand prefab named "RightHandInterctive" in your project (or similar)
