# Realistic Gore

Script mod for People Playground. Every limb gets its own high-res skin texture, and the wounds are painted directly into it, so they don't use the game's low-res wound shader.

## What it adds

Items in the **Realistic** category:

- **Realistic Human (New)** and **Realistic Human (Old)**. New has the more detailed skin.
- **Dogs and cats**. They have fur, skin, fat, muscle, bone and organs.
- **Gore Settings**. Spawn it and use it to open the settings panel. Settings persist between sessions.

What the gore does:

- Bullets make real holes. The cut edges show fat, muscle and bone.
- Heads have a skull and a brain cavity.
- Organs can spill out of large openings. Hearts keep beating for a while.
- A strong enough shot can blow the eye out of its socket.
- Bones break, and compound fractures push through the skin.
- Dismembered limbs leave layered stumps with bone ends.
- Wounds bleed and drip. Bruises spread over time.
- Blood loss makes the skin go pale.
- Dead bodies decay in stages.
- Living bodies slowly heal holes and burns, and leave scars.

## Install

1. Copy the `RealisticGore` folder into `People Playground/Mods/`.
2. Start the game. It compiles the scripts on launch.

The mod needs a build that still loads C# script mods. It was made against 1.27.

## Source

All the code is in the `.cs` files. The game compiles them itself (C# 5, Unity UI only, no reflection or file IO).

| File | What it holds |
|---|---|
| `RealisticGore.cs` | Entry point, item registration, bleeding and damage tuning |
| `GoreSettings.cs`, `GoreSettingsUI.cs` | Settings storage and the panel |
| `RealisticHuman.cs`, `SkinLifelike.cs` | Skin texture generation (normal and lifelike) |
| `Crater.cs` | Per-limb texture component, dismemberment, bone ends |
| `CraterWounds.cs` | Painting holes, craters and head wounds |
| `CraterEffects.cs` | Bleeding, fractures, blood loss, decay, bruises, clotting |
| `CraterOrgans.cs` | Organs, eyes, blood trickles |
| `CraterPieces.cs` | Hanging flesh, pieces falling off |
| `CraterHealing.cs` | Healing and scars |
| `Blood.cs` | Blood droplets, hides the game's own blood |
| `JointSkin.cs` | Joint skin bridge (unused) |
| `FleshHuman.cs` | Flesh strands and spilled organs |
| `Organs.cs`, `OrganPieces.cs` | Organ textures, loose organ pieces |
| `Animals.cs`, `AnimalParts.cs` | Dogs and cats |
