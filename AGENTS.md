# AGENTS.md — AI Code Editor Guidelines for "Yes Chef!"

This document defines the architectural rules, coding standards, and game domain logic for AI agents (Cursor, Windsurf, Claude Code, Copilot) working in this repository.

---

## 1. Project Context & Tech Stack

- **Project Name:** Yes Chef! (MVP)
- **Engine Version:** Unity 6 (`6000.4.12f1` / Unity 6000.x)
- **Language:** C# (.NET 8 / C# 12 features)
- **Render Pipeline:** Universal Render Pipeline (URP) or Built-in Standard
- **Input System:** Unity New Input System (`com.unity.inputsystem`)
- **Third-Party Policy:** **STRICTLY NO THIRD-PARTY PLUGINS.** Use only official Unity Package Manager packages.
- **Platform:** PC Standalone (Fixed Top-Down Camera, 16:9 Aspect Ratio)

---

## 2. Core Architecture & Design Patterns

Follow these patterns strictly when writing or modifying code:

1. **Event-Driven Component Architecture:**
   - Decouple systems using standard C# `event` or `Action<T>` delegates.
   - **Never** poll game state in `Update()` for UI updates. UI components must subscribe to events (e.g., `OnScoreChanged`, `OnTimerUpdated`, `OnOrderCompleted`).

2. **Data-Driven Design via ScriptableObjects:**
   - Ingredients (`IngredientSO`) and game configurations must be ScriptableObjects.
   - Do not hardcode ingredient stats, cook times, or scores inside MonoBehaviour scripts.

3. **Interface-Based Interaction (`IInteractable`):**
   - All kitchen stations (`FridgeStation`, `TableStation`, `StoveStation`, `CustomerWindowStation`, `TrashStation`) must implement `IInteractable`.
   - Player interaction must use spatial queries (`Physics.OverlapSphere` / `SphereCast`) to detect `IInteractable` interfaces.

4. **Finite State Machine (FSM):**
   - The `GameManager` manages global state (`MainMenu`, `Playing`, `Paused`, `GameOver`).

---

## 3. Unity & C# Coding Standards

### Performance & Memory Management
- **Zero Allocations in `Update()`:** No `new` instantiations, string concatenations, or LINQ in frame-rate-dependent methods.
- **Component Caching:** Cache references (`GetComponent<T>()`) in `Awake()` or `Start()`. Never call `GetComponent` or `FindObjectOfType` inside `Update()`.
- **String Hashing:** Use `Animator.StringToHash` and `Shader.PropertyToID` for parameters.
- **Serialized Fields:** Mark private inspectable fields with `[SerializeField] private`. Do not use public fields unless necessary.

### Naming Conventions & Code Style
- **PascalCase:** Class names, Interfaces (`IInteractable`), Methods, Properties, Public fields, Enums.
- **camelCase / `_` Prefix:** Private/protected fields (`_currentScore` or `currentScore`).
- **Namespaces:** Scope all project scripts inside the `YesChef` root namespace (e.g., `namespace YesChef.Stations`).

---

## 4. Domain Logic & Business Rules

### Ingredients & Prep States
- **Enums:**
  - `IngredientType`: `Vegetable`, `Cheese`, `Meat`
  - `PrepState`: `Raw`, `Chopped`, `Cooked`
- **Rules:**
  - **Vegetable:** Base value = `20`. Must be chopped at `TableStation` for **2 seconds**.
  - **Cheese:** Base value = `10`. Requires **no preparation** (`PrepState.Raw`).
  - **Meat:** Base value = `30`. Must be cooked at `StoveStation` for **6 seconds**.

### Player Constraints
- Player can carry a **maximum of 1 ingredient** at a time.
- Movement must feel responsive; carrying an item must not alter movement physics unexpectedly.

### Kitchen Stations Logic
- **FridgeStation:** Spawns a raw `KitchenItem` into player's hand if empty. Never runs out.
- **TableStation:** Accepts 1 Vegetable at a time. Chopping takes 2.0s. Visual timer + material color change when chopped.
- **StoveStation:** Contains **2 independent cooking slots**. Accepts Raw Meat. Each slot cooks for 6.0s hands-off. Visual progress bar per slot + material color change when cooked.
- **CustomerWindowStation:** 4 active windows.
  - Accepts matching prepared items only. If invalid, the player retains the item.
  - When all order ingredients are fulfilled, order completes and window enters a **5.0-second cooldown** before spawning a new order.
- **TrashStation:** Destroys currently held `KitchenItem`.

### Scoring Engine
- **Formula:** 
  $$\text{Order Score} = \sum(\text{Base Ingredient Values}) - \lfloor \text{Elapsed Time in Seconds} \rfloor$$
- **Example:** Meat (30) + Cheese (10) delivered at 14.99 seconds $\rightarrow (30 + 10) - 14 = 26$ points.
- Time deduction docks integer seconds only (`Mathf.FloorToInt(elapsedTime)`).
- High score persists across sessions using `PlayerPrefs` (`"YesChef_HighScore"`).

---