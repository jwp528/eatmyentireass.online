---
name: add-asset-type
description: Complete checklist for adding a new ass type to the game. Use this skill when the user wants to add a new type of ass (new asset type) to the game.
---

# Add Asset Type Skill

Adding a new ass type requires touching multiple files. Follow this checklist completely — missing any step will cause runtime errors or the new type not appearing in-game.

## Required Information

Before starting, confirm:
- **Name**: PascalCase (e.g. `Muscular`, `Sparkle`, `Tiny`)
- **ClicksRequired**: How many canvas bites to fully eat it (int, typically 4–30)
- **HasHole**: Does it show the hole reveal? (`true`/`false`)
- **Points**: Score value (e.g. `0.5`, `1`, `2`, `10`)
- **Bite sound**: `light_crunch.mp3`, `metal_crunch.mp3`, or `bite.mp3`
- **Spawn weight**: Include in normal random pool? Rare (like Golden)? Exclude?
- **Description**: Short flavor text for the carousel (e.g. "2 points. Extra thicc.")

---

## Checklist

### Step 1 — Add to AssTypeEnum (`Shared/Assets.cs`)
```csharp
public enum AssTypeEnum
{
    Boney,
    Cartoon,
    // ...
    NewName,  // ← add here
}
```

### Step 2 — Add to AssTypes list (`Shared/Assets.cs`)
```csharp
public static List<AssTypeEnum> AssTypes = new()
{
    // ...existing...
    AssTypeEnum.NewName,  // ← add here
};
```

### Step 3 — Add Frames entry (`Shared/Assets.cs`)
The `Frames` dict controls the canvas eating sequence. Each entry is:
- `entire_ass.png` — always first
- `chunk1.png` through `chunkN.png` — one per click required
- `hole.png` — add N times if `HasHole = true` (typically 1–5 times at the end)

```csharp
{
    AssTypeEnum.NewName.ToString(), new List<string>
    {
        "entire_ass.png",
        "chunk1.png",
        "chunk2.png",
        // ...up to ClicksRequired chunks
        "hole.png",  // omit if HasHole = false
    }
},
```

> **Note**: The canvas system in `assCanvas.js` uses `destination-out` compositing to erase chunks from `entire_ass.png`. No actual frame files are displayed sequentially — they define bite regions. The file list length determines how many clicks are needed.

### Step 4 — Add to GetRandomAssType() (`Shared/Assets.cs`)
```csharp
// For normal pool (equal weight):
var weightedValues = new[]
{
    // ...existing...
    AssTypeEnum.NewName,  // ← add here
};

// For rare type (like Golden), add to the weighted array manually
// instead of the pool, and give it 1–2 slots out of 50
```

If the type should be rare, follow the Golden pattern — don't add to `weightedValues`, instead manually assign a slot:
```csharp
weightedList[49] = AssTypeEnum.Golden;  // 2% (1 in 50)
// Add more slots to increase rarity tier
```

### Step 5 — Add to GetPointsForAssType() (`Shared/Assets.cs`)
```csharp
public static double GetPointsForAssType(AssTypeEnum assType)
{
    switch (assType)
    {
        case AssTypeEnum.NewName:
            return 2.0;  // ← add case
        // ...
    }
}
```

### Step 6 — Add to GetBiteSoundForAssType() (`Shared/Assets.cs`)
```csharp
var sound = assType switch
{
    AssTypeEnum.Boney => "light_crunch.mp3",
    AssTypeEnum.Golden => "metal_crunch.mp3",
    AssTypeEnum.NewName => "bite.mp3",  // ← add here
    _ => "bite.mp3",
};
```

### Step 7 — Create Image Directory
```
Client/wwwroot/images/Asses/NewName/
├── entire_ass.png       ← required
├── chunk1.png
├── chunk2.png
│   ... (one per click required)
└── hole.png             ← if HasHole = true
```

Create the directory and add a placeholder or real images. The canvas system references `entire_ass.png` directly.

### Step 8 — Add CarouselItem (`Client/Pages/Home.razor.cs`)
Find the `CarouselItems` list and add an entry:
```csharp
new()
{
    ImageUrl = "/images/Asses/NewName/entire_ass.png",
    AltText = "NewName Ass",
    Title = "NewName",
    Description = "2 points. Short flavor description."
},
```

---

## Step 9 — Build & Verify
```bash
dotnet build EMEAOnline.slnx
```
Warnings are OK. Errors indicate a missed step (usually missing enum case in a switch).

---

## Common Mistakes

| Mistake | Result |
|---------|--------|
| Forgot `AssTypes` list | Type never spawns |
| Forgot `Frames` entry | `KeyNotFoundException` at runtime when type is selected |
| Forgot `GetPointsForAssType` case | Falls through to `default` — may give wrong score |
| Image dir missing `entire_ass.png` | Canvas fails to load, game freezes on that ass |
| Chunk count doesn't match `ClicksRequired` | Eating animation ends too early or goes out of bounds |
